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
                idProveedorEspecialidadCotizacion = (int?)x.IdProveedorEspecialidadCotizacion,
                idProyecto = (int?)x.IdProyecto,
                nombreProyecto = (string?)x.NombreProyecto,
                proyecto = (string?)x.NombreProyecto,
                idProveedor = (int?)x.IdProveedor,
                proveedor = (string?)x.Proveedor,
                razonSocial = (string?)x.Proveedor,
                idEspecialidad = (int?)x.IdEspecialidad,
                especialidad = (string?)x.Especialidad,
                nombreEspecialidad = (string?)x.Especialidad,
                servicio = (string?)x.Servicio,
                moneda = (string?)x.Moneda,
                montoCotizacion = (decimal?)x.MontoCotizacion ?? 0m,
                porcentajeGarantia = (decimal?)x.PorcentajeGarantia ?? 0m,
                porcentajeDetraccion = (decimal?)x.PorcentajeDetraccion ?? 0m
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
                dto.Servicio,
                dto.Moneda,
                dto.MontoCotizacion,
                Activo = 1
            }, commandType: CommandType.StoredProcedure);

            return new
            {
                idConfiguracion = (int?)result.IdProveedorEspecialidadCotizacion
            };
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
                nombreProyecto = (string?)x.NombreProyecto,
                proveedor = (string?)x.Proveedor,
                razonSocial = (string?)x.Proveedor,
                especialidad = (string?)x.Especialidad,
                nombreEspecialidad = (string?)x.Especialidad,
                montoCotizacion = (decimal?)x.Cotizacion ?? 0m
            });
        }

        public async Task<object> GetByIdAsync(int idValorizacion)
        {
            using var db = Open();
            using var multi = await db.QueryMultipleAsync(
                "contable.usp_Valorizacion_Get",
                new { IdValorizacion = idValorizacion },
                commandType: CommandType.StoredProcedure);

            var cabeceraRaw = await multi.ReadFirstOrDefaultAsync();
            var detalleRaw = (await multi.ReadAsync()).ToList();
            var resumenRaw = await multi.ReadFirstOrDefaultAsync();

            object? cabecera = null;
            if (cabeceraRaw != null)
            {
                cabecera = new
                {
                    idValorizacion = (int?)cabeceraRaw.IdValorizacion,
                    idConfiguracion = (int?)cabeceraRaw.IdProveedorEspecialidadCotizacion,
                    periodo = (string?)cabeceraRaw.NumeroValorizacion,
                    observacion = (string?)cabeceraRaw.Observacion,
                    proveedor = (string?)cabeceraRaw.Proveedor,
                    especialidad = (string?)cabeceraRaw.Especialidad,
                    proyecto = (string?)cabeceraRaw.NombreProyecto,
                    servicio = (string?)cabeceraRaw.Servicio,
                    moneda = (string?)cabeceraRaw.Moneda,
                    montoCotizacion = (decimal?)cabeceraRaw.Cotizacion ?? 0m,
                    porcentajeGarantia = (decimal?)cabeceraRaw.PorcentajeGarantia ?? 0m,
                    porcentajeDetraccion = (decimal?)cabeceraRaw.PorcentajeDetraccion ?? 0m
                };
            }

            var detalle = detalleRaw.Select(x => new
            {
                idDetalle = (int?)x.IdValorizacionDetalle,
                fechaFactura = (DateTime?)x.FechaFactura,
                numeroFactura = (string?)x.NumeroFactura,
                baseImponible = (decimal?)x.BaseImponible ?? 0m,
                igv = (decimal?)x.Igv ?? 0m,
                montoFactura = (decimal?)x.MontoFactura ?? 0m,
                descripcion = (string?)x.Descripcion,
                detraccion = (decimal?)x.MontoDetraccion ?? 0m,
                garantia = (decimal?)x.MontoGarantia ?? 0m,
                otrosDescuentos = (decimal?)x.OtrosDescuentos ?? 0m,
                aAbonar = (decimal?)x.MontoAbonar ?? 0m,
                fechaTransferencia = (DateTime?)x.FechaTransferencia,
                numeroOperacion = (string?)x.NumeroOperacion,
                bancoTransferencia = (string?)x.BancoTransferencia,
                bancoDestino = (string?)x.BancoDestino,
                montoTransferido = (decimal?)x.MontoTransferido ?? 0m,
                aFavor = (decimal?)x.MontoAFavor ?? 0m,
                deuda = (decimal?)x.MontoDeuda ?? 0m,
                porcentajeAvance = (decimal?)x.PorcentajeAvance ?? 0m,
                porcentajeAcumulado = (decimal?)x.PorcentajeAcumulado ?? 0m,
                porcentajeInicial = (decimal?)x.PorcentajeInicial ?? 0m,
                porcentajeFinal = (decimal?)x.PorcentajeFinal ?? 0m
            });

            object? resumen = null;
            if (resumenRaw != null)
            {
                resumen = new
                {
                    cotizacion = (decimal?)resumenRaw.Cotizacion ?? 0m,
                    garantia = (decimal?)resumenRaw.Garantia ?? 0m,
                    facturado = (decimal?)resumenRaw.Facturado ?? 0m,
                    transferido = (decimal?)resumenRaw.Transferido ?? 0m,
                    resta = (decimal?)resumenRaw.Resta ?? 0m,
                    liquidar = (decimal?)resumenRaw.Liquidar ?? 0m
                };
            }

            return new { cabecera, detalle, resumen };
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
                    pec.Servicio,
                    pec.Moneda,
                    pec.MontoCotizacion,
                    ISNULL(r.PorcentajeGarantia, 0.05) AS PorcentajeGarantia,
                    ISNULL(r.PorcentajeDetraccion, 0.04) AS PorcentajeDetraccion
                  FROM maestra.ProveedorEspecialidadCotizacion pec
                  LEFT JOIN maestra.ProveedorReglaValorizacion r
                    ON r.IdProveedor = pec.IdProveedor
                   AND r.Activo = 1
                  WHERE pec.IdProveedorEspecialidadCotizacion = @IdConfiguracion
                    AND pec.Activo = 1",
                new { dto.IdConfiguracion });

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
                Empresa = (string?)null,
                Servicio = (string?)config.Servicio,
                Moneda = (string?)config.Moneda ?? "Soles",
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
                dto.MontoTransferido
            }, commandType: CommandType.StoredProcedure);

            return new
            {
                idDetalle = (int?)result.IdValorizacionDetalle
            };
        }

        public async Task<object> DeleteDetalleAsync(int idDetalle, string usuario)
        {
            using var db = Open();
            await db.QueryFirstAsync("contable.usp_ValorizacionDetalle_Delete", new
            {
                IdValorizacionDetalle = idDetalle
            }, commandType: CommandType.StoredProcedure);

            return new { ok = true };
        }
    }
}
