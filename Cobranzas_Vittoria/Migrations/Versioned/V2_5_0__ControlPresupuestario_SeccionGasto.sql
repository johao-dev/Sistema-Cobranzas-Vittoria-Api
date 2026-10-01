/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.5.0
Módulo      : Control Presupuestario
Descripción : Secciones de gasto directo y su vínculo con el catálogo de partidas.
===============================================================================

Cada sección corresponde a una pantalla de Operaciones → Gastos del proyecto:

    ADMINISTRATIVO     Gastos administrativos
    TERRENO            Terreno - Anteproyecto - Proyecto
    MARKETING_VENTAS   Marketing / Ventas
    OTROS              Otros gastos
    MUNICIPAL          Gastos municipales y distritales

Una partida hoja puede pertenecer a una sección; así la pantalla de esa sección
solo ofrece sus partidas. Una partida sin sección (IdSeccionGasto NULL) no se
usa para gasto directo: se ejecuta por requerimiento (partidas de obra).

SeccionGastoTipoCentroCosto define qué tipos de centro de costo admite cada
sección (por ejemplo, Gastos administrativos solo trabaja con áreas).

Reentrante: cada objeto e INSERT valida su propia ausencia.
===============================================================================
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'ControlPresupuestario.SeccionGasto', N'U') IS NULL
BEGIN
    CREATE TABLE ControlPresupuestario.SeccionGasto
    (
        IdSeccionGasto INT IDENTITY(1,1) NOT NULL,
        Codigo VARCHAR(30) NOT NULL,
        Nombre NVARCHAR(100) NOT NULL,
        Descripcion NVARCHAR(255) NULL,
        Orden INT NOT NULL,
        Activo BIT NOT NULL CONSTRAINT DF_SeccionGasto_Activo DEFAULT (1),

        CONSTRAINT PK_SeccionGasto PRIMARY KEY CLUSTERED (IdSeccionGasto),
        CONSTRAINT UQ_SeccionGasto_Codigo UNIQUE (Codigo)
    );
END;

IF OBJECT_ID(N'ControlPresupuestario.SeccionGastoTipoCentroCosto', N'U') IS NULL
BEGIN
    CREATE TABLE ControlPresupuestario.SeccionGastoTipoCentroCosto
    (
        IdSeccionGasto INT NOT NULL,
        IdTipoCentroCosto INT NOT NULL,

        CONSTRAINT PK_SeccionGastoTipoCentroCosto PRIMARY KEY CLUSTERED (IdSeccionGasto, IdTipoCentroCosto),
        CONSTRAINT FK_SeccionGastoTipoCentroCosto_Seccion FOREIGN KEY (IdSeccionGasto)
            REFERENCES ControlPresupuestario.SeccionGasto (IdSeccionGasto),
        CONSTRAINT FK_SeccionGastoTipoCentroCosto_Tipo FOREIGN KEY (IdTipoCentroCosto)
            REFERENCES ControlPresupuestario.TipoCentroCosto (IdTipoCentroCosto)
    );
END;

DECLARE @Secciones TABLE
(
    Codigo VARCHAR(30) NOT NULL PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL,
    Descripcion NVARCHAR(255) NOT NULL,
    Orden INT NOT NULL
);

INSERT INTO @Secciones (Codigo, Nombre, Descripcion, Orden) VALUES
    ('ADMINISTRATIVO', N'Gastos administrativos',
     N'Gastos de oficina y áreas de la empresa: alquiler, servicios, planilla, bancos.', 1),
    ('TERRENO', N'Terreno - Anteproyecto - Proyecto',
     N'Compra de terreno, alcabala, estudios previos y desarrollo del proyecto.', 2),
    ('MARKETING_VENTAS', N'Marketing / Ventas',
     N'Publicidad, marketing, comisiones por ventas y sala de ventas.', 3),
    ('OTROS', N'Otros gastos',
     N'Gastos del proyecto que no pertenecen a otra sección.', 4),
    ('MUNICIPAL', N'Gastos municipales y distritales',
     N'Licencias, independización, declaratoria, conformidad e instalaciones.', 5);

INSERT INTO ControlPresupuestario.SeccionGasto (Codigo, Nombre, Descripcion, Orden)
SELECT s.Codigo, s.Nombre, s.Descripcion, s.Orden
FROM @Secciones s
WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.SeccionGasto e WHERE e.Codigo = s.Codigo);

-- Tipos de centro de costo admitidos por sección.
-- Gastos administrativos: todas las áreas (todo tipo salvo PROYECTO).
-- Marketing / Ventas: proyectos y las áreas de marketing y ventas.
-- Resto: solo proyectos.
DECLARE @Matriz TABLE
(
    CodigoSeccion VARCHAR(30) NOT NULL,
    CodigoTipo VARCHAR(50) NOT NULL,
    PRIMARY KEY (CodigoSeccion, CodigoTipo)
);

INSERT INTO @Matriz (CodigoSeccion, CodigoTipo)
SELECT 'ADMINISTRATIVO', t.Codigo
FROM ControlPresupuestario.TipoCentroCosto t
WHERE t.Codigo <> 'PROYECTO';

INSERT INTO @Matriz (CodigoSeccion, CodigoTipo) VALUES
    ('TERRENO', 'PROYECTO'),
    ('MARKETING_VENTAS', 'PROYECTO'),
    ('MARKETING_VENTAS', 'MARKETING'),
    ('MARKETING_VENTAS', 'VENTAS'),
    ('OTROS', 'PROYECTO'),
    ('MUNICIPAL', 'PROYECTO');

INSERT INTO ControlPresupuestario.SeccionGastoTipoCentroCosto (IdSeccionGasto, IdTipoCentroCosto)
SELECT s.IdSeccionGasto, t.IdTipoCentroCosto
FROM @Matriz m
JOIN ControlPresupuestario.SeccionGasto s ON s.Codigo = m.CodigoSeccion
JOIN ControlPresupuestario.TipoCentroCosto t ON t.Codigo = m.CodigoTipo
WHERE NOT EXISTS (
    SELECT 1 FROM ControlPresupuestario.SeccionGastoTipoCentroCosto e
    WHERE e.IdSeccionGasto = s.IdSeccionGasto AND e.IdTipoCentroCosto = t.IdTipoCentroCosto
);

IF COL_LENGTH(N'ControlPresupuestario.CatalogoPartida', N'IdSeccionGasto') IS NULL
BEGIN
    ALTER TABLE ControlPresupuestario.CatalogoPartida
        ADD IdSeccionGasto INT NULL
            CONSTRAINT FK_CatalogoPartida_SeccionGasto
            REFERENCES ControlPresupuestario.SeccionGasto (IdSeccionGasto);
END;

COMMIT TRANSACTION;
GO

-- Índice en lote aparte: la columna debe existir antes de compilar la sentencia.
-- Sin filtro a propósito: en SQL Server 2025 un índice filtrado sobre la columna
-- de una FK impide compilar el plan del DELETE sobre la tabla referenciada (Msg 8624).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CatalogoPartida_IdSeccionGasto'
      AND object_id = OBJECT_ID(N'ControlPresupuestario.CatalogoPartida')
)
    CREATE NONCLUSTERED INDEX IX_CatalogoPartida_IdSeccionGasto
    ON ControlPresupuestario.CatalogoPartida (IdSeccionGasto)
    INCLUDE (Codigo, Nombre, Activo);
GO
