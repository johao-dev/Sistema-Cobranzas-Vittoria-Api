/*
  Conciliación READ-ONLY de CategoriaGasto para GastoDirecto.
  No inserta, no actualiza y no genera movimientos presupuestales.
*/
SET NOCOUNT ON;

-- Resumen de cobertura del backfill.
SELECT COUNT(*) AS TotalGastosDirectos,
       SUM(CASE WHEN gd.IdCategoriaGasto IS NOT NULL THEN 1 ELSE 0 END) AS Clasificados,
       SUM(CASE WHEN gd.IdCategoriaGasto IS NULL THEN 1 ELSE 0 END) AS Pendientes
FROM contable.GastoDirecto gd;

-- Categorías preexistentes cuyo código estable requiere decisión manual.
SELECT IdCategoriaGasto, Nombre, Activo,
       'CATEGORIA_SIN_CODIGO_ESTABLE' AS Motivo
FROM maestra.CategoriaGasto
WHERE Codigo IS NULL
ORDER BY IdCategoriaGasto;

-- Gastos que no tuvieron evidencia inequívoca para el backfill.
SELECT gd.IdGastoDirecto,
       lm.Origen AS OrigenLegacy,
       lm.IdLegacy,
       ga.IdCategoriaGasto AS IdCategoriaLegacy,
       cga.Nombre AS CategoriaLegacy,
       gp.TipoModulo,
       gd.Concepto,
       gd.Fecha,
       gd.Monto,
       cc.Codigo AS CodigoCentroCosto,
       cc.Nombre AS CentroCosto,
       cp.Codigo AS CodigoPartida,
       cp.Nombre AS Partida,
       CASE
         WHEN lm.IdGastoDirecto IS NULL THEN 'SIN_ORIGEN_LEGACY_CONFIABLE'
         WHEN lm.Origen = 'GASTO_PROYECTO'
              AND LOWER(REPLACE(REPLACE(LTRIM(RTRIM(gp.TipoModulo)), '-', ''), ' ', '')) = 'terreno'
           THEN 'MODULO_TERRENO_NO_DISTINGUE_TERRENO_ANTEPROYECTO_PROYECTO'
         WHEN lm.Origen = 'GASTO_PROYECTO' THEN 'TIPO_MODULO_NO_MAPEABLE'
         WHEN lm.Origen = 'GASTO_ADMIN' THEN 'ORIGEN_ADMINISTRATIVO_INCOMPLETO'
         ELSE 'SIN_EVIDENCIA_CLASIFICATORIA'
       END AS Motivo
FROM contable.GastoDirecto gd
LEFT JOIN contable.GastoDirectoLegacyMap lm ON lm.IdGastoDirecto = gd.IdGastoDirecto
LEFT JOIN contable.GastoProyecto gp
  ON lm.Origen = 'GASTO_PROYECTO' AND gp.IdGastoProyecto = lm.IdLegacy
LEFT JOIN contable.GastoAdministrativo ga
  ON lm.Origen = 'GASTO_ADMIN' AND ga.IdGastoAdministrativo = lm.IdLegacy
LEFT JOIN maestra.CategoriaGasto cga ON cga.IdCategoriaGasto = ga.IdCategoriaGasto
JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoDetalle = gd.IdPresupuestoDetalle
JOIN ControlPresupuestario.CatalogoPartida cp ON cp.IdCatalogoPartida = pd.IdCatalogoPartida
JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = pv.IdPresupuesto
JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
WHERE gd.IdCategoriaGasto IS NULL
ORDER BY gd.Fecha, gd.IdGastoDirecto;
