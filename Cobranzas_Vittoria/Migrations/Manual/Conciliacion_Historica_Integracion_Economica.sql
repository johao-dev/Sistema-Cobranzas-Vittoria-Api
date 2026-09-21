/*
  CONCILIACIÓN HISTÓRICA PREVIA A V2.2.0

  Este script es deliberadamente de solo lectura. No reconstruye movimientos ni
  decide equivalencias de estados sin evidencia documental. Ejecútelo en una
  copia restaurada de producción, exporte todos los result sets y resuelva cada
  observación antes de volver a iniciar DbUp.
*/
SET NOCOUNT ON;

SELECT 'ESTADOS_OC_NO_CANONICOS' AS Hallazgo,
       oc.IdOrdenCompra, oc.NumeroOrdenCompra, oc.Estado,
       oc.FechaOrdenCompra, oc.Total
FROM compras.OrdenCompra oc
WHERE UPPER(LTRIM(RTRIM(oc.Estado))) NOT IN
    ('REGISTRADA', 'APROBADA', 'ATENDIDA', 'CERRADA', 'ANULADA')
ORDER BY oc.IdOrdenCompra;

SELECT 'MULTIPLES_COMPRAS_POR_OC' AS Hallazgo,
       c.IdOrdenCompra, COUNT(*) AS CantidadCompras,
       STRING_AGG(CONVERT(VARCHAR(MAX), c.IdCompra), ',') AS IdCompras
FROM compras.Compra c
GROUP BY c.IdOrdenCompra
HAVING COUNT(*) > 1
ORDER BY c.IdOrdenCompra;

SELECT 'OC_SIN_PARTIDA_EN_TODAS_SUS_LINEAS' AS Hallazgo,
       oc.IdOrdenCompra, oc.NumeroOrdenCompra, od.IdMaterial,
       r.IdRequerimiento, r.NumeroRequerimiento, rd.IdRequerimientoDetalle
FROM compras.OrdenCompra oc
JOIN compras.OrdenCompraDetalle od ON od.IdOrdenCompra = oc.IdOrdenCompra
JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN compras.RequerimientoDetalle rd
    ON rd.IdRequerimiento = r.IdRequerimiento AND rd.IdMaterial = od.IdMaterial
WHERE rd.IdRequerimientoDetalle IS NULL OR rd.IdPresupuestoDetalle IS NULL
ORDER BY oc.IdOrdenCompra, od.IdMaterial;

IF COL_LENGTH('ControlPresupuestario.CentroCosto', 'IdProyecto') IS NULL
BEGIN
    SELECT 'CENTRO_COSTO_PROYECTO_REQUIERE_MAPEO' AS Hallazgo,
           cc.IdCentroCosto, cc.Codigo, cc.Nombre, tc.Codigo AS TipoCentroCosto,
           CAST(NULL AS INT) AS IdProyecto
    FROM ControlPresupuestario.CentroCosto cc
    JOIN ControlPresupuestario.TipoCentroCosto tc
        ON tc.IdTipoCentroCosto = cc.IdTipoCentroCosto
    WHERE tc.Codigo = 'PROYECTO'
    ORDER BY cc.IdCentroCosto;
END
ELSE
BEGIN
    EXEC sys.sp_executesql N'
        SELECT ''CENTRO_COSTO_PROYECTO_SIN_MAPEO'' AS Hallazgo,
               cc.IdCentroCosto, cc.Codigo, cc.Nombre, tc.Codigo AS TipoCentroCosto,
               cc.IdProyecto
        FROM ControlPresupuestario.CentroCosto cc
        JOIN ControlPresupuestario.TipoCentroCosto tc
            ON tc.IdTipoCentroCosto = cc.IdTipoCentroCosto
        WHERE tc.Codigo = ''PROYECTO'' AND cc.IdProyecto IS NULL
        ORDER BY cc.IdCentroCosto;

        SELECT ''PARTIDA_NO_COINCIDE_CON_PROYECTO_O_MONEDA'' AS Hallazgo,
               oc.IdOrdenCompra, od.IdMaterial, r.IdProyecto AS ProyectoCompra,
               cc.IdProyecto AS ProyectoPresupuesto, oc.IdMoneda AS MonedaOc,
               p.IdMoneda AS MonedaPresupuesto, rd.IdPresupuestoDetalle
        FROM compras.OrdenCompra oc
        JOIN compras.OrdenCompraDetalle od ON od.IdOrdenCompra = oc.IdOrdenCompra
        JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
        JOIN compras.RequerimientoDetalle rd
            ON rd.IdRequerimiento = r.IdRequerimiento AND rd.IdMaterial = od.IdMaterial
        JOIN ControlPresupuestario.PresupuestoDetalle pd
            ON pd.IdPresupuestoDetalle = rd.IdPresupuestoDetalle
        JOIN ControlPresupuestario.PresupuestoVersion pv
            ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
        JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = pv.IdPresupuesto
        JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
        WHERE cc.IdProyecto IS NULL OR cc.IdProyecto <> r.IdProyecto OR p.IdMoneda <> oc.IdMoneda
        ORDER BY oc.IdOrdenCompra, od.IdMaterial;';
END;

SELECT 'EVENTOS_ECONOMICOS_ESPERADOS_SIN_RECONSTRUIR' AS Hallazgo,
       oc.IdOrdenCompra, oc.Estado AS EstadoOc, od.IdMaterial,
       rd.IdPresupuestoDetalle, od.Subtotal AS CompromisoDocumental,
       c.IdCompra, c.Aceptada, cd.Subtotal AS EjecucionDocumental,
       CONCAT('OC:', oc.IdOrdenCompra, ':APROBACION:MATERIAL:', od.IdMaterial) AS ClaveCompromisoSugerida,
       CASE WHEN c.IdCompra IS NULL THEN NULL ELSE
           CONCAT('COMPRA:', c.IdCompra, ':MATERIAL:', od.IdMaterial, ':LIBERACION') END AS ClaveLiberacionSugerida,
       CASE WHEN c.IdCompra IS NULL THEN NULL ELSE
           CONCAT('COMPRA:', c.IdCompra, ':MATERIAL:', od.IdMaterial, ':EJECUCION') END AS ClaveEjecucionSugerida
FROM compras.OrdenCompra oc
JOIN compras.OrdenCompraDetalle od ON od.IdOrdenCompra = oc.IdOrdenCompra
JOIN compras.RequerimientoDetalle rd
    ON rd.IdRequerimiento = oc.IdRequerimiento AND rd.IdMaterial = od.IdMaterial
LEFT JOIN compras.Compra c ON c.IdOrdenCompra = oc.IdOrdenCompra
LEFT JOIN compras.CompraDetalle cd
    ON cd.IdCompra = c.IdCompra AND cd.IdMaterial = od.IdMaterial
WHERE rd.IdPresupuestoDetalle IS NOT NULL
ORDER BY oc.IdOrdenCompra, od.IdMaterial, c.IdCompra;

SELECT 'MOVIMIENTOS_EXISTENTES_POR_ORIGEN' AS Hallazgo,
       mp.Origen, mp.IdOrigen, tm.Codigo AS TipoMovimiento,
       COUNT(*) AS Cantidad, SUM(mp.Monto) AS Monto
FROM ControlPresupuestario.MovimientoPresupuestal mp
JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
    ON tm.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
WHERE mp.Origen IN ('ORDEN_COMPRA', 'COMPRA')
GROUP BY mp.Origen, mp.IdOrigen, tm.Codigo
ORDER BY mp.Origen, mp.IdOrigen, tm.Codigo;

/*
  Criterio de cierre de la conciliación:
  1. Cada estado no canónico tiene una decisión documentada hacia uno de los
     cinco estados admitidos.
  2. Existe como máximo una Compra definitiva por OC.
  3. Cada línea operativa tiene IdPresupuestoDetalle demostrado.
  4. CentroCosto.IdProyecto y la moneda coinciden con Requerimiento/OC.
  5. Toda carga manual al ledger usa ClaveEvento única y queda respaldada por
     acta o documento fuente. Este archivo no debe convertirse en un UPDATE/INSERT
     automático para producción.
*/
