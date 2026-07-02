CREATE OR ALTER VIEW [compras].[vw_Compra_ListadoConEspecialidad]
AS
SELECT
    c.IdCompra,
    c.NumeroCompra,
    c.FechaCompra,
    CASE
        WHEN c.Aceptada = 1 THEN 'Aceptada'
        ELSE 'Pendiente'
    END AS Estado,
    c.IncluyeIGV,
    c.SubtotalSinIGV,
    c.MontoIGV,
    c.MontoTotal,
    c.Observacion,
    p.RazonSocial AS Proveedor,
    e.Nombre AS Especialidad,
    pr.NombreProyecto,
    oc.NumeroOrdenCompra,
    r.NumeroRequerimiento
FROM
    compras.Compra c
INNER JOIN compras.OrdenCompra oc
    ON
    oc.IdOrdenCompra = c.IdOrdenCompra
LEFT JOIN compras.Requerimiento r
    ON
    r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proveedor p
    ON
    p.IdProveedor = c.IdProveedor
LEFT JOIN maestra.Especialidad e
    ON
    e.IdEspecialidad = r.IdEspecialidad
LEFT JOIN maestra.Proyecto pr
    ON
    pr.IdProyecto = COALESCE(oc.IdProyecto, r.IdProyecto);
