/*
  Diagnóstico READ-ONLY previo/posterior a la consolidación contable.
  No corrige datos, no asigna partidas y no genera movimientos económicos.
*/
SET NOCOUNT ON;

-- RUC canónicos duplicados que impedirían la garantía física.
SELECT LTRIM(RTRIM(Ruc)) AS Ruc, COUNT(*) AS Cantidad
FROM maestra.Proveedor
WHERE NULLIF(LTRIM(RTRIM(Ruc)), '') IS NOT NULL
GROUP BY LTRIM(RTRIM(Ruc))
HAVING COUNT(*) > 1;

-- Documentos legacy ausentes, sentinela o no confiables para matching automático.
SELECT 'PROVEEDOR_TERRENO' AS Origen, IdProveedorTerreno AS IdLegacy,
    RazonSocial, Ruc
FROM maestra.ProveedorTerreno
WHERE NULLIF(LTRIM(RTRIM(Ruc)), '') IS NULL
   OR LEN(LTRIM(RTRIM(Ruc))) <> 11
   OR LTRIM(RTRIM(Ruc)) LIKE '%[^0-9]%'
   OR LTRIM(RTRIM(Ruc)) = REPLICATE('0', 11)
UNION ALL
SELECT 'PROVEEDOR_GASTO_ADMIN', IdProveedorGastoAdministrativo,
    RazonSocial, Ruc
FROM maestra.ProveedorGastoAdministrativo
WHERE NULLIF(LTRIM(RTRIM(Ruc)), '') IS NULL
   OR LEN(LTRIM(RTRIM(Ruc))) <> 11
   OR LTRIM(RTRIM(Ruc)) LIKE '%[^0-9]%'
   OR LTRIM(RTRIM(Ruc)) = REPLICATE('0', 11);

-- Datos que excederían el catálogo canónico (debe quedar vacío tras ampliar columnas).
SELECT 'PROVEEDOR_TERRENO' AS Origen, IdProveedorTerreno AS IdLegacy,
    LEN(RazonSocial) AS LargoRazonSocial, LEN(Telefono) AS LargoTelefono
FROM maestra.ProveedorTerreno
WHERE LEN(RazonSocial) > 250 OR LEN(Telefono) > 50
UNION ALL
SELECT 'PROVEEDOR_GASTO_ADMIN', IdProveedorGastoAdministrativo,
    LEN(RazonSocial), LEN(Telefono)
FROM maestra.ProveedorGastoAdministrativo
WHERE LEN(RazonSocial) > 250 OR LEN(Telefono) > 50;

-- Gastos que no pueden migrarse sin inventar una partida, moneda o monto.
SELECT 'GASTO_PROYECTO' AS Origen, gp.IdGastoProyecto AS IdLegacy,
    CASE
      WHEN gp.IdPresupuestoDetalle IS NULL THEN 'SIN_PRESUPUESTO_DETALLE'
      WHEN UPPER(LTRIM(RTRIM(gp.Moneda))) NOT IN ('PEN','USD') THEN 'MONEDA_NO_NORMALIZABLE'
      WHEN m.IdMoneda IS NULL THEN 'MONEDA_NO_EXISTE'
      WHEN p.IdMoneda <> m.IdMoneda THEN 'MONEDA_INCOMPATIBLE_CON_PRESUPUESTO'
      WHEN CASE WHEN UPPER(LTRIM(RTRIM(gp.Moneda)))='USD' THEN gp.MontoDolares ELSE gp.MontoSoles END <= 0 THEN 'MONTO_NO_POSITIVO'
    END AS Motivo
FROM contable.GastoProyecto gp
LEFT JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoDetalle=gp.IdPresupuestoDetalle
LEFT JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuestoVersion=pd.IdPresupuestoVersion
LEFT JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto=pv.IdPresupuesto
LEFT JOIN maestra.Moneda m ON m.Codigo=UPPER(LTRIM(RTRIM(gp.Moneda)))
WHERE gp.IdPresupuestoDetalle IS NULL OR pd.IdPresupuestoDetalle IS NULL
   OR UPPER(LTRIM(RTRIM(gp.Moneda))) NOT IN ('PEN','USD')
   OR m.IdMoneda IS NULL OR p.IdMoneda<>m.IdMoneda
   OR CASE WHEN UPPER(LTRIM(RTRIM(gp.Moneda)))='USD' THEN gp.MontoDolares ELSE gp.MontoSoles END <= 0
UNION ALL
SELECT 'GASTO_ADMIN', ga.IdGastoAdministrativo,
    CASE
      WHEN ga.IdPresupuestoDetalle IS NULL THEN 'SIN_PRESUPUESTO_DETALLE'
      WHEN UPPER(LTRIM(RTRIM(ga.Moneda))) NOT IN ('PEN','USD') THEN 'MONEDA_NO_NORMALIZABLE'
      WHEN m.IdMoneda IS NULL THEN 'MONEDA_NO_EXISTE'
      WHEN p.IdMoneda <> m.IdMoneda THEN 'MONEDA_INCOMPATIBLE_CON_PRESUPUESTO'
      WHEN ga.Monto <= 0 THEN 'MONTO_NO_POSITIVO'
    END
FROM contable.GastoAdministrativo ga
LEFT JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoDetalle=ga.IdPresupuestoDetalle
LEFT JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuestoVersion=pd.IdPresupuestoVersion
LEFT JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto=pv.IdPresupuesto
LEFT JOIN maestra.Moneda m ON m.Codigo=UPPER(LTRIM(RTRIM(ga.Moneda)))
WHERE ga.IdPresupuestoDetalle IS NULL OR pd.IdPresupuestoDetalle IS NULL
   OR UPPER(LTRIM(RTRIM(ga.Moneda))) NOT IN ('PEN','USD')
   OR m.IdMoneda IS NULL OR p.IdMoneda<>m.IdMoneda OR ga.Monto<=0;

-- Documentos cuyo padre no pudo migrarse automáticamente.
SELECT 'GASTO_PROYECTO' AS Origen, d.IdGastoProyectoDocumento AS IdDocumento,
    d.IdGastoProyecto AS IdPadre, d.RutaArchivo
FROM contable.GastoProyectoDocumento d
LEFT JOIN contable.GastoDirectoLegacyMap m
  ON m.Origen='GASTO_PROYECTO' AND m.IdLegacy=d.IdGastoProyecto
WHERE m.IdGastoDirecto IS NULL
UNION ALL
SELECT 'GASTO_ADMIN', d.IdGastoAdministrativoDocumento,
    d.IdGastoAdministrativo, d.RutaArchivo
FROM contable.GastoAdministrativoDocumento d
LEFT JOIN contable.GastoDirectoLegacyMap m
  ON m.Origen='GASTO_ADMIN' AND m.IdLegacy=d.IdGastoAdministrativo
WHERE m.IdGastoDirecto IS NULL;
