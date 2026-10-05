using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>
/// Única corrección manual admitida sobre el ledger: un asiento AJUSTE. Es una acción de negocio
/// explícita, no un CRUD de movimientos. La ClaveEvento se deriva del detalle, la dimensión y el
/// instante UTC: un reenvío accidental del mismo formulario no duplica el asiento, pero una
/// corrección posterior legítima sí se registra.
/// </summary>
public sealed class AjustePresupuestal
{
    public const string OrigenAjusteManual = "AJUSTE_MANUAL";

    public int IdPresupuestoDetalle { get; }
    public AfectacionPresupuestal Afectacion { get; }
    public DireccionMovimiento Direccion { get; }
    public decimal Monto { get; }
    public string Observacion { get; }
    public DateTime? Fecha { get; }
    public string ClaveEvento { get; }
    public string Origen => OrigenAjusteManual;
    public int IdOrigen => IdPresupuestoDetalle;

    private AjustePresupuestal(int idPresupuestoDetalle, AfectacionPresupuestal afectacion, DireccionMovimiento direccion,
        decimal monto, string observacion, DateTime? fecha, string claveEvento)
    {
        IdPresupuestoDetalle = idPresupuestoDetalle;
        Afectacion = afectacion;
        Direccion = direccion;
        Monto = monto;
        Observacion = observacion;
        Fecha = fecha;
        ClaveEvento = claveEvento;
    }

    public static AjustePresupuestal Crear(int idPresupuestoDetalle, string? afectacion, string? direccion, decimal monto,
        string? observacion, DateTime? fecha, DateTime ahoraUtc)
    {
        Reglas.Id(idPresupuestoDetalle, "IdPresupuestoDetalle");
        var af = AfectacionPresupuestal.Crear(afectacion);
        var dir = DireccionMovimiento.Crear(direccion);
        if (monto <= 0)
            throw new ValidacionPresupuestariaException("MONTO_INVALIDO", "El monto del ajuste debe ser positivo.");
        if (string.IsNullOrWhiteSpace(observacion))
            throw new ValidacionPresupuestariaException("OBSERVACION_REQUERIDA",
                "Un ajuste manual requiere una observación que lo justifique.");
        var justificacion = Reglas.Requerido(observacion, "Observacion", 500);
        var momento = (fecha ?? ahoraUtc).ToUniversalTime();
        var clave = $"{OrigenAjusteManual}:{idPresupuestoDetalle}:{af.Valor}:{dir.Valor}:{momento:yyyyMMddHHmmssfff}";
        return new AjustePresupuestal(idPresupuestoDetalle, af, dir, decimal.Round(monto, 2, MidpointRounding.AwayFromZero),
            justificacion, fecha, clave);
    }

    /// <summary>
    /// Un ajuste que INCREMENTA consume presupuesto igual que una ejecución, así que no puede dejar
    /// la partida excedida. Un DECREMENTO libera saldo y siempre se admite: es el mecanismo de
    /// corrección del ledger append-only.
    /// </summary>
    public void ValidarContraSaldo(decimal? saldoDisponible)
    {
        if (Direccion != DireccionMovimiento.Incremento || saldoDisponible is null) return;
        if (Monto > saldoDisponible.Value)
            throw new ControlPresupuestarioException("SALDO_INSUFICIENTE",
                $"El ajuste de {Monto:N2} dejaría la partida excedida: el saldo disponible es {saldoDisponible.Value:N2}.");
    }
}
