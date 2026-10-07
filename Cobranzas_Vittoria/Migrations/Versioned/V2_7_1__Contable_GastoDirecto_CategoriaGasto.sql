/*
===============================================================================
Versión     : V2.7.1
Módulo      : Contable / Gasto directo
Descripción : Persiste la categoría descriptiva de cada GastoDirecto.

La categoría no participa en la imputación presupuestaria. El backfill usa
únicamente GastoDirectoLegacyMap y el registro legacy original. Los módulos
Terreno no se clasifican porque esa pantalla contenía TERRENO, ANTEPROYECTO y
PROYECTO y la fuente histórica no permite distinguirlos.
===============================================================================
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Identidad de negocio del catálogo existente. No se recrean filas ni IDs.
IF COL_LENGTH(N'maestra.CategoriaGasto', N'Codigo') IS NULL
    ALTER TABLE maestra.CategoriaGasto ADD Codigo VARCHAR(30) NULL;
GO

IF EXISTS
(
    SELECT m.Codigo
    FROM maestra.CategoriaGasto c
    JOIN (VALUES
        (N'OTROS GASTOS',       'OTROS'),
        (N'MARKETING Y VENTAS', 'MARKETING_VENTAS'),
        (N'TERRENO',            'TERRENO'),
        (N'ANTEPROYECTO',       'ANTEPROYECTO'),
        (N'PROYECTO',           'PROYECTO'),
        (N'GASTOS ADMINISTRATIVOS', 'ADMINISTRATIVO'),
        (N'GASTOS MUNICIPALES', 'MUNICIPAL')
    ) m(Nombre, Codigo) ON UPPER(LTRIM(RTRIM(c.Nombre))) = m.Nombre
    GROUP BY m.Codigo
    HAVING COUNT(*) > 1
)
    THROW 51710, 'CATEGORIA_GASTO_AMBIGUA: hay nombres conocidos duplicados; conciliar antes de asignar Codigo.', 1;

UPDATE c
SET Codigo = m.Codigo
FROM maestra.CategoriaGasto c
JOIN (VALUES
    (N'OTROS GASTOS',       'OTROS'),
    (N'MARKETING Y VENTAS', 'MARKETING_VENTAS'),
    (N'TERRENO',            'TERRENO'),
    (N'ANTEPROYECTO',       'ANTEPROYECTO'),
    (N'PROYECTO',           'PROYECTO'),
    (N'GASTOS ADMINISTRATIVOS', 'ADMINISTRATIVO'),
    (N'GASTOS MUNICIPALES', 'MUNICIPAL')
) m(Nombre, Codigo) ON UPPER(LTRIM(RTRIM(c.Nombre))) = m.Nombre
WHERE c.Codigo IS NULL;

-- En instalaciones conocidas las siete filas quedan normalizadas y la columna
-- puede ser NOT NULL. Categorías adicionales no inferibles conservan NULL para
-- conciliación explícita, sin impedir el upgrade.
IF NOT EXISTS (SELECT 1 FROM maestra.CategoriaGasto WHERE Codigo IS NULL)
    ALTER TABLE maestra.CategoriaGasto ALTER COLUMN Codigo VARCHAR(30) NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE parent_object_id = OBJECT_ID(N'maestra.CategoriaGasto')
                 AND name = N'CK_CategoriaGasto_Codigo')
    ALTER TABLE maestra.CategoriaGasto ADD CONSTRAINT CK_CategoriaGasto_Codigo
        CHECK (Codigo IS NULL OR LEN(LTRIM(RTRIM(Codigo))) > 0);

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'maestra.CategoriaGasto')
                 AND name = N'UX_CategoriaGasto_Codigo')
    CREATE UNIQUE INDEX UX_CategoriaGasto_Codigo
        ON maestra.CategoriaGasto(Codigo) WHERE Codigo IS NOT NULL;

-- El TVP también incorpora la identidad estable. Se retira únicamente el SP
-- repeatable conocido antes de comprobar consumidores inesperados.
DROP PROCEDURE IF EXISTS maestra.usp_CategoriaGasto_CargaMasiva;

IF TYPE_ID(N'maestra.TVP_CategoriaGasto') IS NOT NULL
   AND EXISTS
   (
       SELECT 1 FROM sys.parameters
       WHERE user_type_id = TYPE_ID(N'maestra.TVP_CategoriaGasto')
   )
    THROW 51711, 'DEPENDENCIA_INESPERADA: existe un objeto no reconocido que usa TVP_CategoriaGasto.', 1;

IF TYPE_ID(N'maestra.TVP_CategoriaGasto') IS NOT NULL
    DROP TYPE maestra.TVP_CategoriaGasto;

EXEC(N'CREATE TYPE maestra.TVP_CategoriaGasto AS TABLE
(
    Codigo VARCHAR(30) NOT NULL,
    Nombre NVARCHAR(150) NOT NULL,
    Activo BIT NOT NULL,
    _Fila  INT NOT NULL
)');

-- La asociación en GastoDirecto nace nullable para no inventar historia.
IF COL_LENGTH(N'contable.GastoDirecto', N'IdCategoriaGasto') IS NULL
    ALTER TABLE contable.GastoDirecto ADD IdCategoriaGasto INT NULL;
GO

-- Evidencia directa: el gasto administrativo conserva su categoría original.
UPDATE gd
SET IdCategoriaGasto = ga.IdCategoriaGasto
FROM contable.GastoDirecto gd
JOIN contable.GastoDirectoLegacyMap lm
  ON lm.IdGastoDirecto = gd.IdGastoDirecto AND lm.Origen = 'GASTO_ADMIN'
JOIN contable.GastoAdministrativo ga ON ga.IdGastoAdministrativo = lm.IdLegacy
JOIN maestra.CategoriaGasto cg ON cg.IdCategoriaGasto = ga.IdCategoriaGasto
WHERE gd.IdCategoriaGasto IS NULL;

-- Evidencia inequívoca de GastoProyecto. 'Terreno' se omite deliberadamente:
-- esa pantalla agrupaba TERRENO, ANTEPROYECTO y PROYECTO.
UPDATE gd
SET IdCategoriaGasto = cg.IdCategoriaGasto
FROM contable.GastoDirecto gd
JOIN contable.GastoDirectoLegacyMap lm
  ON lm.IdGastoDirecto = gd.IdGastoDirecto AND lm.Origen = 'GASTO_PROYECTO'
JOIN contable.GastoProyecto gp ON gp.IdGastoProyecto = lm.IdLegacy
CROSS APPLY
(
    SELECT CASE LOWER(REPLACE(REPLACE(LTRIM(RTRIM(gp.TipoModulo)), '-', ''), ' ', ''))
        WHEN 'marketing' THEN 'MARKETING_VENTAS'
        WHEN 'gastosmunicipales' THEN 'MUNICIPAL'
        WHEN 'otrosgastos' THEN 'OTROS'
        ELSE NULL END AS Codigo
) evidencia
JOIN maestra.CategoriaGasto cg ON cg.Codigo = evidencia.Codigo
WHERE gd.IdCategoriaGasto IS NULL AND evidencia.Codigo IS NOT NULL;

-- Solo una instalación sin historia ambigua obtiene integridad NOT NULL física.
IF NOT EXISTS (SELECT 1 FROM contable.GastoDirecto WHERE IdCategoriaGasto IS NULL)
    ALTER TABLE contable.GastoDirecto ALTER COLUMN IdCategoriaGasto INT NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'contable.GastoDirecto')
                 AND name = N'FK_GastoDirecto_CategoriaGasto')
    ALTER TABLE contable.GastoDirecto WITH CHECK ADD CONSTRAINT FK_GastoDirecto_CategoriaGasto
        FOREIGN KEY (IdCategoriaGasto) REFERENCES maestra.CategoriaGasto(IdCategoriaGasto);

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'contable.GastoDirecto')
                 AND name = N'IX_GastoDirecto_Categoria_Fecha')
    CREATE INDEX IX_GastoDirecto_Categoria_Fecha
        ON contable.GastoDirecto(IdCategoriaGasto, Fecha DESC, IdGastoDirecto DESC);

COMMIT TRANSACTION;
GO
