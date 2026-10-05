using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.RegistrarAjuste;

/// <summary>
/// Registra un ajuste manual sobre una partida de versión. usp_MovimientoPresupuestal_Registrar no
/// valida saldo (el bloqueo de partida excedida vive en el flujo de gasto directo), así que la regla
/// de saldo del ajuste la aplica el modelo AjustePresupuestal.
/// </summary>
public sealed class RegistrarAjusteHandler
{
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;
    private readonly IConsultaPresupuestariaRepository _consultas;
    private readonly IMovimientoPresupuestalRepository _movimientos;
    private readonly TimeProvider _reloj;
    private readonly ILogger<RegistrarAjusteHandler> _logger;

    public RegistrarAjusteHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles,
        IConsultaPresupuestariaRepository consultas, IMovimientoPresupuestalRepository movimientos, TimeProvider reloj,
        ILogger<RegistrarAjusteHandler> logger)
    {
        _versiones = versiones;
        _detalles = detalles;
        _consultas = consultas;
        _movimientos = movimientos;
        _reloj = reloj;
        _logger = logger;
    }

    public async Task<MovimientoPresupuestalResult> HandleAsync(RegistrarAjusteCommand c)
    {
        PresupuestoDetalleValidator.ValidarIds(c.IdPresupuesto, c.IdPresupuestoVersion, c.IdPresupuestoDetalle);
        await _versiones.ObtenerDelPresupuestoAsync(c.IdPresupuesto, c.IdPresupuestoVersion);
        await _detalles.ObtenerDeLaVersionAsync(c.IdPresupuestoVersion, c.IdPresupuestoDetalle);

        var ajuste = AjustePresupuestal.Crear(c.IdPresupuestoDetalle, c.Afectacion, c.Direccion, c.Monto, c.Observacion,
            c.Fecha, _reloj.GetUtcNow().UtcDateTime);
        var saldo = await _consultas.SaldoDetalleAsync(c.IdPresupuestoDetalle);
        ajuste.ValidarContraSaldo(saldo?.SaldoDisponible);

        var movimiento = await _movimientos.RegistrarAjusteAsync(ajuste)
            ?? throw new ControlPresupuestarioException("AJUSTE_NO_CONFIRMADO",
                "El ajuste no devolvió un movimiento; revise el ledger antes de reintentar.");
        _logger.LogInformation("Ajuste registrado: IdMovimiento={Id}, IdPresupuestoDetalle={Detalle}, {Direccion} {Monto}",
            movimiento.IdMovimientoPresupuestal, c.IdPresupuestoDetalle, ajuste.Direccion, ajuste.Monto);
        return MovimientoPresupuestalResult.Desde(movimiento);
    }
}
