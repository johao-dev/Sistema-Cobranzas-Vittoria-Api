/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.5.7
Módulo      : Contable / Gasto directo por sección
Descripción : Recupera funciones de las pantallas antiguas de Gastos del proyecto.
===============================================================================

1) GastoDirecto.FechaTipoCambio: fecha del tipo de cambio de la factura en otra
   moneda (referencia, como la "Fecha TC" de las pantallas antiguas). Solo tiene
   sentido junto con la moneda original.

2) SeccionGastoCategoriaGasto: relaciona las categorías de gasto antiguas
   (maestra.CategoriaGasto) con las secciones. Permite ofrecer primero, en cada
   sección, los proveedores que antes pertenecían a sus categorías (vía
   maestra.ProveedorLegacyMap). Los proveedores de terreno van a TERRENO.

Reentrante: cada objeto e INSERT valida su propia ausencia.
===============================================================================
*/

-- La tabla nace en V2.3.1; se protege para bases que aún no la tienen.
IF OBJECT_ID(N'contable.GastoDirecto', N'U') IS NOT NULL
   AND COL_LENGTH(N'contable.GastoDirecto', N'FechaTipoCambio') IS NULL
    ALTER TABLE contable.GastoDirecto ADD FechaTipoCambio DATE NULL;
GO

-- EXEC dinámico: la sentencia solo se compila si la columna existe.
IF COL_LENGTH(N'contable.GastoDirecto', N'FechaTipoCambio') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_GastoDirecto_FechaTipoCambio')
    EXEC (N'
    ALTER TABLE contable.GastoDirecto
        ADD CONSTRAINT CK_GastoDirecto_FechaTipoCambio
        CHECK (FechaTipoCambio IS NULL OR IdMonedaOriginal IS NOT NULL);');
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'ControlPresupuestario.SeccionGastoCategoriaGasto', N'U') IS NULL
BEGIN
    CREATE TABLE ControlPresupuestario.SeccionGastoCategoriaGasto
    (
        IdSeccionGasto INT NOT NULL,
        IdCategoriaGasto INT NOT NULL,

        CONSTRAINT PK_SeccionGastoCategoriaGasto PRIMARY KEY CLUSTERED (IdCategoriaGasto),
        CONSTRAINT FK_SeccionGastoCategoriaGasto_Seccion FOREIGN KEY (IdSeccionGasto)
            REFERENCES ControlPresupuestario.SeccionGasto (IdSeccionGasto),
        CONSTRAINT FK_SeccionGastoCategoriaGasto_Categoria FOREIGN KEY (IdCategoriaGasto)
            REFERENCES maestra.CategoriaGasto (IdCategoriaGasto)
    );
END;

-- Por nombre: los Id de CategoriaGasto pueden variar entre ambientes.
INSERT INTO ControlPresupuestario.SeccionGastoCategoriaGasto (IdSeccionGasto, IdCategoriaGasto)
SELECT s.IdSeccionGasto, c.IdCategoriaGasto
FROM (VALUES
    (N'GASTOS ADMINISTRATIVOS', 'ADMINISTRATIVO'),
    (N'TERRENO', 'TERRENO'),
    (N'ANTEPROYECTO', 'TERRENO'),
    (N'PROYECTO', 'TERRENO'),
    (N'MARKETING Y VENTAS', 'MARKETING_VENTAS'),
    (N'OTROS GASTOS', 'OTROS'),
    (N'GASTOS MUNICIPALES', 'MUNICIPAL')
) m (NombreCategoria, CodigoSeccion)
JOIN maestra.CategoriaGasto c ON UPPER(LTRIM(RTRIM(c.Nombre))) = m.NombreCategoria
JOIN ControlPresupuestario.SeccionGasto s ON s.Codigo = m.CodigoSeccion
WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.SeccionGastoCategoriaGasto x
                  WHERE x.IdCategoriaGasto = c.IdCategoriaGasto);

COMMIT TRANSACTION;
