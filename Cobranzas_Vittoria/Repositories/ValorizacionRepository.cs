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
            var rows = await db.QueryAsync("maestra.usp_ProveedorEspecialidadCotizacion_List", new
            {
                IdProyecto = idProyecto,
                IdProveedor = idProveedor,
                IdEspecialidad = idEspecialidad
            }, commandType: CommandType.StoredProcedure);

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
                porcentajeGarantia = (decimal?)x.PorcentajeGarantia ?? 0.04m,
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

            var cabecera = await multi.ReadFirstOrDefaultAsync();
            var detalleRaw = (await multi.ReadAsync()).ToList();
            var resumen = await multi.ReadFirstOrDefaultAsync();
            var archivosRaw = (await multi.ReadAsync()).ToList();

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
                fechaFactura = (DateTime?)d.FechaFactura,
                numeroFactura = (string?)d.NumeroFactura,
                montoFactura = (decimal?)d.MontoFactura ?? 0m,
                descripcion = (string?)d.Descripcion,
                detraccion = (decimal?)d.Detraccion ?? 0m,
                garantia = (decimal?)d.Garantia ?? 0m,
                montoTransferido = (decimal?)d.MontoTransferido ?? 0m,
                fechaTransferencia = (DateTime?)d.FechaTransferencia,
                archivos = archivos.Where(a => a.idDetalle == (int?)d.IdValorizacionDetalle).ToList()
            });

            return new
            {
                cabecera = cabecera == null ? null : new
                {
                    idValorizacion = (int?)cabecera.IdValorizacion,
                    idConfiguracion = (int?)cabecera.IdProveedorEspecialidadCotizacion,
                    periodo = (string?)cabecera.NumeroValorizacion,
                    proyecto = (string?)cabecera.NombreProyecto,
                    proveedor = (string?)cabecera.Proveedor,
                    especialidad = (string?)cabecera.Especialidad,
                    empresa = (string?)cabecera.Empresa,
                    servicio = (string?)cabecera.Servicio,
                    moneda = (string?)cabecera.Moneda,
                    cotizacion = (decimal?)cabecera.Cotizacion ?? 0m,
                    porcentajeGarantia = (decimal?)cabecera.PorcentajeGarantia ?? 0.04m,
                    porcentajeDetraccion = (decimal?)cabecera.PorcentajeDetraccion ?? 0.04m,
                    observacion = (string?)cabecera.Observacion
                },
                detalle,
                resumen = resumen == null ? null : new
                {
                    cotizacion = (decimal?)resumen.Cotizacion ?? 0m,
                    garantia = (decimal?)resumen.GarantiaRetenida ?? 0m,
                    facturado = (decimal?)resumen.Facturado ?? 0m,
                    transferido = (decimal?)resumen.Transferido ?? 0m,
                    resta = (decimal?)resumen.Resta ?? 0m,
                    liquidar = (decimal?)resumen.Liquidar ?? 0m
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
                    ISNULL(r.PorcentajeGarantia, 0.04) AS PorcentajeGarantia,
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
