using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Valorizaciones;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories
{
    public class ValorizacionRepository : RepositoryBase, IValorizacionRepository
    {
        public ValorizacionRepository(IDbConnectionFactory factory) : base(factory) { }

        public async Task<object> ListConfiguracionesAsync(int? idProyecto, int? idProveedor, int? idEspecialidad)
        {
            using var db = Open();
            var rows = await db.QueryAsync(@"SELECT
                    pec.IdProveedorEspecialidadCotizacion,
                    pec.IdProyecto,
                    p.NombreProyecto,
                    pec.IdProveedor,
                    pr.RazonSocial AS Proveedor,
                    pec.IdEspecialidad,
                    e.Nombre AS Especialidad,
                    pec.Empresa,
                    pec.Servicio,
                    pec.Moneda,
                    pec.MontoCotizacion,
                    ISNULL(rv.PorcentajeGarantia, 0.05) AS PorcentajeGarantia,
                    ISNULL(rv.PorcentajeDetraccion, 0.04) AS PorcentajeDetraccion
                FROM maestra.ProveedorEspecialidadCotizacion pec
                INNER JOIN maestra.Proveedor pr ON pr.IdProveedor = pec.IdProveedor
                INNER JOIN maestra.Especialidad e ON e.IdEspecialidad = pec.IdEspecialidad
                LEFT JOIN maestra.Proyecto p ON p.IdProyecto = pec.IdProyecto
                LEFT JOIN maestra.ProveedorReglaValorizacion rv ON rv.IdProveedor = pec.IdProveedor AND rv.Activo = 1
                WHERE pec.Activo = 1
                  AND (@IdProyecto IS NULL OR pec.IdProyecto = @IdProyecto)
                  AND (@IdProveedor IS NULL OR pec.IdProveedor = @IdProveedor)
                  AND (@IdEspecialidad IS NULL OR pec.IdEspecialidad = @IdEspecialidad)
                ORDER BY pec.IdProveedorEspecialidadCotizacion DESC;",
                new { IdProyecto = idProyecto, IdProveedor = idProveedor, IdEspecialidad = idEspecialidad });

            return rows.Select(x => new
            {
                idConfiguracion = (int?)x.IdProveedorEspecialidadCotizacion,
                idProyecto = (int?)x.IdProyecto,
                proyecto = (string?)x.NombreProyecto,
                idProveedor = (int?)x.IdProveedor,
                proveedor = (string?)x.Proveedor,
                idEspecialidad = (int?)x.IdEspecialidad,
                especialidad = (string?)x.Especialidad,
                empresa = (string?)x.Empresa,
                servicio = (string?)x.Servicio,
                moneda = (string?)x.Moneda,
                montoCotizacion = (decimal?)x.MontoCotizacion ?? 0m,
                porcentajeGarantia = (decimal?)x.PorcentajeGarantia ?? 0.05m,
                porcentajeDetraccion = (decimal?)x.PorcentajeDetraccion ?? 0.04m
            });
        }

        public async Task<object> UpsertConfiguracionAsync(ProveedorEspecialidadCotizacionUpsertDto dto)
        {
            using var db = Open();
            var result = await db.QueryFirstAsync("maestra.usp_ProveedorEspecialidadCotizacion_Upsert", new
            {
                IdProveedorEspecialidadCotizacion = dto.IdConfiguracion,
                dto.IdProyecto,
                dto.IdProveedor,
                dto.IdEspecialidad,
                dto.Empresa,
                dto.Servicio,
                dto.Moneda,
                dto.MontoCotizacion,
                Activo = 1
            }, commandType: CommandType.StoredProcedure);

            return new { idConfiguracion = (int?)result.IdProveedorEspecialidadCotizacion };
        }

        public async Task<object> UpsertReglaProveedorAsync(ProveedorReglaValorizacionUpsertDto dto)
        {
            using var db = Open();
            return await db.QueryFirstAsync("maestra.usp_ProveedorReglaValorizacion_Upsert", new
            {
                dto.IdProveedor,
                dto.PorcentajeGarantia,
                dto.PorcentajeDetraccion,
                dto.Usuario
            }, commandType: CommandType.StoredProcedure);
        }

        public async Task<object> ListAsync(int? idProyecto, int? idProveedor, int? idEspecialidad)
        {
            using var db = Open();
            var rows = await db.QueryAsync("contable.usp_Valorizacion_List", new
            {
                IdProyecto = idProyecto,
                IdProveedor = idProveedor,
                IdEspecialidad = idEspecialidad
            }, commandType: CommandType.StoredProcedure);

            return rows.Select(x => new
            {
                idValorizacion = (int?)x.IdValorizacion,
                periodo = (string?)x.NumeroValorizacion,
                proyecto = (string?)x.NombreProyecto,
                proveedor = (string?)x.Proveedor,
                especialidad = (string?)x.Especialidad,
                empresa = (string?)x.Empresa,
                servicio = (string?)x.Servicio,
                moneda = (string?)x.Moneda,
                cotizacion = (decimal?)x.Cotizacion ?? 0m,
                facturado = (decimal?)x.Facturado ?? 0m,
                transferido = (decimal?)x.Transferido ?? 0m,
                garantia = (decimal?)x.GarantiaRetenida ?? 0m,
                detraccion = (decimal?)x.DetraccionAcumulada ?? 0m,
                resta = (decimal?)x.Resta ?? 0m,
                liquidar = (decimal?)x.Liquidar ?? 0m
            });
        }

        public async Task<object> GetByIdAsync(int idValorizacion)
        {
            using var db = Open();
            using var multi = await db.QueryMultipleAsync("contable.usp_Valorizacion_Get", new { IdValorizacion = idValorizacion }, commandType: CommandType.StoredProcedure);

            var cabeceraRaw = await multi.ReadFirstOrDefaultAsync();
            await multi.ReadAsync(); // Detalle del SP original, se reemplaza por histórico filtrado
            await multi.ReadAsync(); // Resumen del SP original, se recalcula
            await multi.ReadAsync(); // Archivos del SP original, se reemplazan por histórico filtrado

            if (cabeceraRaw == null)
            {
                return new { cabecera = (object?)null, detalle = Array.Empty<object>(), resumen = (object?)null };
            }

            var idProveedor = (int?)cabeceraRaw.IdProveedor;
            var idEspecialidad = (int?)cabeceraRaw.IdEspecialidad;
            var cotizacion = (decimal?)cabeceraRaw.Cotizacion ?? 0m;

            var cabecera = new
            {
                idValorizacion = (int?)cabeceraRaw.IdValorizacion,
                idConfiguracion = (int?)cabeceraRaw.IdProveedorEspecialidadCotizacion,
                periodo = (string?)cabeceraRaw.NumeroValorizacion,
                proyecto = (string?)cabeceraRaw.NombreProyecto,
                proveedor = (string?)cabeceraRaw.Proveedor,
                especialidad = (string?)cabeceraRaw.Especialidad,
                empresa = (string?)cabeceraRaw.Empresa,
                servicio = (string?)cabeceraRaw.Servicio,
                moneda = (string?)cabeceraRaw.Moneda,
                cotizacion,
                porcentajeGarantia = (decimal?)cabeceraRaw.PorcentajeGarantia ?? 0.05m,
                porcentajeDetraccion = (decimal?)cabeceraRaw.PorcentajeDetraccion ?? 0.04m,
                observacion = (string?)cabeceraRaw.Observacion
            };

            if (!idProveedor.HasValue || !idEspecialidad.HasValue)
            {
                return new
                {
                    cabecera,
                    detalle = Array.Empty<object>(),
                    resumen = new
                    {
                        cotizacion,
                        garantia = 0m,
                        facturado = 0m,
                        transferido = 0m,
                        resta = 0m,
                        liquidar = 0m
                    }
                };
            }

            var detalleResumenRaw = (await db.QueryAsync(@"SELECT
                    vd.MontoFactura,
                    CAST(ROUND(vd.MontoFactura * ISNULL(vd.PorcentajeGarantiaAplicado, 0), 2) AS DECIMAL(18,2)) AS Garantia,
                    vd.MontoAbonar,
                    vd.MontoDeuda,
                    vd.MontoTransferido
                FROM contable.ValorizacionDetalle vd
                INNER JOIN contable.Valorizacion v ON v.IdValorizacion = vd.IdValorizacion
                WHERE v.Activo = 1
                  AND vd.Activo = 1
                  AND v.IdProveedor = @IdProveedor
                  AND v.IdEspecialidad = @IdEspecialidad;",
                new { IdProveedor = idProveedor, IdEspecialidad = idEspecialidad })).ToList();

            var detalleRaw = (await db.QueryAsync(@"SELECT
                    vd.IdValorizacionDetalle,
                    vd.IdValorizacion,
                    vd.FechaFactura,
                    vd.NumeroFactura,
                    vd.MontoFactura,
                    vd.Descripcion,
                    CAST(ROUND(vd.MontoFactura * ISNULL(vd.PorcentajeDetraccionAplicado, 0), 0) AS DECIMAL(18,2)) AS Detraccion,
                    CAST(ROUND(vd.MontoFactura * ISNULL(vd.PorcentajeGarantiaAplicado, 0), 2) AS DECIMAL(18,2)) AS Garantia,
                    vd.MontoAbonar,
                    vd.MontoDeuda,
                    vd.MontoTransferido,
                    vd.FechaTransferencia,
                    pr.RazonSocial AS Proveedor,
                    e.Nombre AS Especialidad,
                    v.NumeroValorizacion
                FROM contable.ValorizacionDetalle vd
                INNER JOIN contable.Valorizacion v ON v.IdValorizacion = vd.IdValorizacion
                INNER JOIN maestra.Proveedor pr ON pr.IdProveedor = v.IdProveedor
                INNER JOIN maestra.Especialidad e ON e.IdEspecialidad = v.IdEspecialidad
                WHERE v.Activo = 1
                  AND vd.Activo = 1
                  AND v.IdProveedor = @IdProveedor
                ORDER BY ISNULL(vd.FechaFactura, '19000101') DESC, vd.IdValorizacionDetalle DESC;",
                new { IdProveedor = idProveedor })).ToList();

            var archivosRaw = (await db.QueryAsync(@"SELECT
                    a.IdValorizacionDetalleArchivo,
                    a.IdValorizacionDetalle,
                    a.NombreArchivo,
                    a.RutaArchivo,
                    a.Extension
                FROM contable.ValorizacionDetalleArchivo a
                INNER JOIN contable.ValorizacionDetalle vd ON vd.IdValorizacionDetalle = a.IdValorizacionDetalle
                INNER JOIN contable.Valorizacion v ON v.IdValorizacion = vd.IdValorizacion
                WHERE v.Activo = 1
                  AND vd.Activo = 1
                  AND v.IdProveedor = @IdProveedor
                ORDER BY a.IdValorizacionDetalle, a.IdValorizacionDetalleArchivo;",
                new { IdProveedor = idProveedor })).ToList();

            var archivos = archivosRaw.Select(a => new
            {
                idArchivo = (int?)a.IdValorizacionDetalleArchivo,
                idDetalle = (int?)a.IdValorizacionDetalle,
                nombreArchivo = (string?)a.NombreArchivo,
                rutaArchivo = (string?)a.RutaArchivo,
                extension = (string?)a.Extension
            }).ToList();

            var detalle = detalleRaw.Select(d => new
            {
                idDetalle = (int?)d.IdValorizacionDetalle,
                idValorizacion = (int?)d.IdValorizacion,
                periodo = (string?)d.NumeroValorizacion,
                proveedor = (string?)d.Proveedor,
                especialidad = (string?)d.Especialidad,
                fechaFactura = (DateTime?)d.FechaFactura,
                numeroFactura = (string?)d.NumeroFactura,
                montoFactura = (decimal?)d.MontoFactura ?? 0m,
                descripcion = (string?)d.Descripcion,
                detraccion = (decimal?)d.Detraccion ?? 0m,
                garantia = (decimal?)d.Garantia ?? 0m,
                montoAbonar = (decimal?)d.MontoAbonar ?? 0m,
                montoDeuda = (decimal?)d.MontoDeuda ?? 0m,
                montoTransferido = (decimal?)d.MontoTransferido ?? 0m,
                fechaTransferencia = (DateTime?)d.FechaTransferencia,
                archivos = archivos.Where(a => a.idDetalle == (int?)d.IdValorizacionDetalle).ToList()
            }).ToList();

            var facturado = detalleResumenRaw.Sum(x => (decimal?)x.MontoFactura ?? 0m);
            var garantia = detalleResumenRaw.Sum(x => (decimal?)x.Garantia ?? 0m);
            var transferido = detalleResumenRaw.Sum(x => (decimal?)x.MontoTransferido ?? 0m);
            var resta = detalleResumenRaw.Sum(x => (decimal?)x.MontoDeuda ?? 0m);
            var liquidar = detalleResumenRaw.Sum(x => (decimal?)x.MontoAbonar ?? 0m);

            return new
            {
                cabecera,
                detalle,
                resumen = new
                {
                    cotizacion,
                    garantia,
                    facturado,
                    transferido,
                    resta,
                    liquidar
                }
            };
        }

        public async Task<object> UpsertAsync(ValorizacionUpsertDto dto)
        {
            using var db = Open();
            var config = await db.QueryFirstOrDefaultAsync(
                @"SELECT
                    pec.IdProveedorEspecialidadCotizacion,
                    pec.IdProyecto,
                    pec.IdProveedor,
                    pec.IdEspecialidad,
                    pec.Empresa,
                    pec.Servicio,
                    pec.Moneda,
                    pec.MontoCotizacion,
                    ISNULL(r.PorcentajeGarantia, 0.05) AS PorcentajeGarantia,
                    ISNULL(r.PorcentajeDetraccion, 0.04) AS PorcentajeDetraccion
                  FROM maestra.ProveedorEspecialidadCotizacion pec
                  LEFT JOIN maestra.ProveedorReglaValorizacion r
                    ON r.IdProveedor = pec.IdProveedor AND r.Activo = 1
                  WHERE pec.IdProveedorEspecialidadCotizacion = @IdConfiguracion
                    AND pec.Activo = 1", new { dto.IdConfiguracion });

            if (config == null)
                throw new InvalidOperationException("No se encontró la configuración seleccionada para la valorización.");

            var numeroValorizacion = string.IsNullOrWhiteSpace(dto.Periodo)
                ? $"VAL-{dto.IdConfiguracion}-{DateTime.Now:yyyyMMddHHmmss}"
                : dto.Periodo.Trim();

            var result = await db.QueryFirstAsync("contable.usp_Valorizacion_Upsert", new
            {
                dto.IdValorizacion,
                NumeroValorizacion = numeroValorizacion,
                IdProyecto = (int?)config.IdProyecto,
                IdProveedor = (int)config.IdProveedor,
                IdEspecialidad = (int)config.IdEspecialidad,
                IdProveedorEspecialidadCotizacion = (int?)config.IdProveedorEspecialidadCotizacion,
                Empresa = string.IsNullOrWhiteSpace(dto.Empresa) ? (string?)config.Empresa : dto.Empresa,
                Servicio = (string?)config.Servicio,
                Moneda = (string?)config.Moneda ?? "PEN",
                Cotizacion = (decimal)config.MontoCotizacion,
                PorcentajeGarantia = (decimal)config.PorcentajeGarantia,
                PorcentajeDetraccion = (decimal)config.PorcentajeDetraccion,
                dto.Observacion
            }, commandType: CommandType.StoredProcedure);

            return new
            {
                idValorizacion = (int?)result.IdValorizacion,
                numeroValorizacion = (string?)result.NumeroValorizacion
            };
        }

        public async Task<object> UpsertDetalleAsync(ValorizacionDetalleUpsertDto dto)
        {
            using var db = Open();
            var result = await db.QueryFirstAsync("contable.usp_ValorizacionDetalle_Upsert", new
            {
                IdValorizacionDetalle = dto.IdDetalle,
                dto.IdValorizacion,
                dto.FechaFactura,
                dto.NumeroFactura,
                dto.MontoFactura,
                dto.Descripcion,
                dto.OtrosDescuentos,
                dto.FechaTransferencia,
                dto.NumeroOperacion,
                dto.BancoTransferencia,
                dto.BancoDestino,
                dto.MontoTransferido,
                dto.PorcentajeDetraccionAplicado,
                dto.PorcentajeGarantiaAplicado
            }, commandType: CommandType.StoredProcedure);

            return new { idDetalle = (int?)result.IdValorizacionDetalle };
        }

        public async Task<object> DeleteDetalleAsync(int idDetalle, string usuario)
        {
            using var db = Open();
            await db.QueryFirstAsync("contable.usp_ValorizacionDetalle_Delete", new { IdValorizacionDetalle = idDetalle }, commandType: CommandType.StoredProcedure);
            return new { ok = true };
        }
    }
}
