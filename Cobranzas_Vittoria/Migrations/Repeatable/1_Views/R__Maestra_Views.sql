CREATE OR ALTER VIEW [maestra].[vw_ProveedorEspecialidadCotizacionResumen]
AS
SELECT
    pec.IdProveedorEspecialidadCotizacion,
    pec.IdProyecto,
    p.NombreProyecto,
    pec.IdProveedor,
    pr.RazonSocial AS Proveedor,
    pec.IdEspecialidad,
    e.Nombre AS Especialidad,
    pec.Servicio,
    pec.Moneda,
    pec.MontoCotizacion,
    ISNULL(rv.PorcentajeGarantia, 0.050000) AS PorcentajeGarantia,
    ISNULL(rv.PorcentajeDetraccion, 0.040000) AS PorcentajeDetraccion,
    pec.Activo,
    pec.FechaCreacion
FROM
    maestra.ProveedorEspecialidadCotizacion pec
INNER JOIN maestra.Proveedor pr
    ON
    pr.IdProveedor = pec.IdProveedor
INNER JOIN maestra.Especialidad e
    ON
    e.IdEspecialidad = pec.IdEspecialidad
LEFT JOIN maestra.Proyecto p
    ON
    p.IdProyecto = pec.IdProyecto
LEFT JOIN maestra.ProveedorReglaValorizacion rv
    ON
    rv.IdProveedor = pec.IdProveedor
    AND rv.Activo = 1
WHERE
    pec.Activo = 1;
