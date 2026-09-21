/* Fase estructural reintentable para la integración económica Compras/Presupuesto. */

IF COL_LENGTH('ControlPresupuestario.CentroCosto', 'IdProyecto') IS NULL
    ALTER TABLE ControlPresupuestario.CentroCosto ADD IdProyecto INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CentroCosto_Proyecto'
               AND parent_object_id = OBJECT_ID('ControlPresupuestario.CentroCosto'))
    ALTER TABLE ControlPresupuestario.CentroCosto WITH CHECK
        ADD CONSTRAINT FK_CentroCosto_Proyecto FOREIGN KEY (IdProyecto)
        REFERENCES maestra.Proyecto(IdProyecto);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ControlPresupuestario.CentroCosto')
               AND name = 'UX_CentroCosto_IdProyecto')
    CREATE UNIQUE INDEX UX_CentroCosto_IdProyecto
        ON ControlPresupuestario.CentroCosto(IdProyecto)
        WHERE IdProyecto IS NOT NULL;
GO

IF COL_LENGTH('compras.OrdenCompra', 'VersionEconomica') IS NULL
    ALTER TABLE compras.OrdenCompra ADD VersionEconomica INT NOT NULL
        CONSTRAINT DF_OrdenCompra_VersionEconomica DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_OrdenCompra_VersionEconomica')
    ALTER TABLE compras.OrdenCompra WITH CHECK ADD CONSTRAINT CK_OrdenCompra_VersionEconomica
        CHECK (VersionEconomica >= 0);
GO

IF COL_LENGTH('compras.Compra', 'Estado') IS NULL
    ALTER TABLE compras.Compra ADD Estado VARCHAR(20) NULL;
GO

UPDATE compras.Compra
SET Estado = CASE WHEN Aceptada = 1 THEN 'ACEPTADA' ELSE 'REGISTRADA' END
WHERE Estado IS NULL;

IF EXISTS (SELECT 1 FROM compras.Compra WHERE Estado IS NULL)
    THROW 51400, 'No se pudo determinar el estado histórico de todas las Compras.', 1;

ALTER TABLE compras.Compra ALTER COLUMN Estado VARCHAR(20) NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints
               WHERE parent_object_id = OBJECT_ID('compras.Compra') AND name = 'DF_Compra_Estado')
    ALTER TABLE compras.Compra ADD CONSTRAINT DF_Compra_Estado DEFAULT ('REGISTRADA') FOR Estado;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Compra_Estado')
    ALTER TABLE compras.Compra WITH CHECK ADD CONSTRAINT CK_Compra_Estado
        CHECK (Estado IN ('REGISTRADA', 'ACEPTADA'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Compra_Estado_Aceptada')
    ALTER TABLE compras.Compra WITH CHECK ADD CONSTRAINT CK_Compra_Estado_Aceptada
        CHECK ((Estado = 'ACEPTADA' AND Aceptada = 1)
            OR (Estado = 'REGISTRADA' AND Aceptada = 0));
GO

/* Retirar el CHECK legacy antes de auditar estados para que, si la migración
   se detiene, el operador pueda conciliar ACEPTADA hacia APROBADA sin tener
   que descubrir y eliminar manualmente una restricción de nombre variable. */
DECLARE @restriccionEstadoLegacy SYSNAME;
SELECT @restriccionEstadoLegacy = cc.name
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('compras.OrdenCompra')
  AND cc.definition LIKE '%Estado%'
  AND cc.name <> 'CK_OrdenCompra_Estado_Economico';
IF @restriccionEstadoLegacy IS NOT NULL
BEGIN
    DECLARE @sqlDropEstadoLegacy NVARCHAR(500) =
        N'ALTER TABLE compras.OrdenCompra DROP CONSTRAINT ' + QUOTENAME(@restriccionEstadoLegacy) + N';';
    EXEC sys.sp_executesql @sqlDropEstadoLegacy;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM compras.OrdenCompra
    WHERE UPPER(LTRIM(RTRIM(Estado))) NOT IN
        ('REGISTRADA', 'APROBADA', 'ATENDIDA', 'CERRADA', 'ANULADA')
)
BEGIN
    DECLARE @estadosPendientes NVARCHAR(MAX) =
    (
        SELECT IdOrdenCompra, NumeroOrdenCompra, Estado
        FROM compras.OrdenCompra
        WHERE UPPER(LTRIM(RTRIM(Estado))) NOT IN
            ('REGISTRADA', 'APROBADA', 'ATENDIDA', 'CERRADA', 'ANULADA')
        FOR JSON PATH
    );
    PRINT N'CONCILIACION_OC_ESTADO_PENDIENTE: ' + LEFT(@estadosPendientes, 3500);
    THROW 51401, 'Hay estados históricos de OC ambiguos. Conciliarlos antes de activar la máquina de estados económica.', 1;
END;

UPDATE compras.OrdenCompra SET Estado = UPPER(LTRIM(RTRIM(Estado)));

DECLARE @restriccionEstado SYSNAME;
SELECT @restriccionEstado = cc.name
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('compras.OrdenCompra')
  AND cc.definition LIKE '%Estado%';

IF @restriccionEstado IS NOT NULL AND @restriccionEstado <> 'CK_OrdenCompra_Estado_Economico'
BEGIN
    DECLARE @sqlDropEstado NVARCHAR(500) =
        N'ALTER TABLE compras.OrdenCompra DROP CONSTRAINT ' + QUOTENAME(@restriccionEstado) + N';';
    EXEC sys.sp_executesql @sqlDropEstado;
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_OrdenCompra_Estado_Economico')
    ALTER TABLE compras.OrdenCompra WITH CHECK ADD CONSTRAINT CK_OrdenCompra_Estado_Economico
        CHECK (Estado IN ('REGISTRADA', 'APROBADA', 'ATENDIDA', 'CERRADA', 'ANULADA'));
GO

IF EXISTS
(
    SELECT IdOrdenCompra
    FROM compras.Compra
    GROUP BY IdOrdenCompra
    HAVING COUNT(*) > 1
)
BEGIN
    DECLARE @comprasDuplicadas NVARCHAR(MAX) =
    (
        SELECT IdOrdenCompra, COUNT(*) AS CantidadCompras
        FROM compras.Compra
        GROUP BY IdOrdenCompra
        HAVING COUNT(*) > 1
        FOR JSON PATH
    );
    PRINT N'CONCILIACION_OC_MULTIPLES_COMPRAS: ' + LEFT(@comprasDuplicadas, 3500);
    THROW 51402, 'Hay OC históricas con más de una Compra. Conciliarlas antes de exigir una Compra única por OC.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('compras.Compra')
               AND name = 'UX_Compra_IdOrdenCompra')
    CREATE UNIQUE INDEX UX_Compra_IdOrdenCompra ON compras.Compra(IdOrdenCompra);
GO

IF TYPE_ID('compras.TVP_MovimientoEconomico') IS NULL
    EXEC(N'CREATE TYPE compras.TVP_MovimientoEconomico AS TABLE
    (
        IdPresupuestoDetalle INT NOT NULL,
        TipoMovimiento VARCHAR(30) NOT NULL,
        ClaveEvento VARCHAR(200) NOT NULL,
        Origen VARCHAR(50) NOT NULL,
        IdOrigen INT NOT NULL,
        Monto DECIMAL(18,2) NOT NULL,
        Fecha DATETIME2(0) NULL,
        Observacion NVARCHAR(500) NULL
    );');
GO
