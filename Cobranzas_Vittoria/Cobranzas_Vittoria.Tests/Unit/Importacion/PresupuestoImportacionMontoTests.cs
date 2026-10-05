using Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Importar;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion;

/// <summary>
/// Lectura de montos del CSV de presupuesto: en Perú se escribe tanto con punto
/// como con coma decimal, y una coma decimal nunca debe leerse como separador de miles.
/// </summary>
public class PresupuestoImportacionMontoTests
{
    [TestCase("15000.50", 15000.50)]
    [TestCase("15000,50", 15000.50)]
    [TestCase("15000,5", 15000.5)]
    [TestCase("15,000", 15000)]
    [TestCase("12,345.67", 12345.67)]
    [TestCase("12.345,67", 12345.67)]
    [TestCase("1.250.000", 1250000)]
    [TestCase("1,250,000", 1250000)]
    [TestCase("S/ 2,500.00", 2500)]
    [TestCase("0", 0)]
    [TestCase("-5", -5)]
    public void TryLeerMonto_FormatosAceptados(string texto, decimal esperado)
    {
        Assert.That(MontoImportado.TryLeer(texto, out var monto), Is.True);
        Assert.That(monto, Is.EqualTo(esperado));
    }

    [TestCase("abc")]
    [TestCase("12a")]
    [TestCase("")]
    [TestCase("1.2.3,4,5")]
    public void TryLeerMonto_TextoInvalido_DevuelveFalse(string texto)
        => Assert.That(MontoImportado.TryLeer(texto, out _), Is.False);
}
