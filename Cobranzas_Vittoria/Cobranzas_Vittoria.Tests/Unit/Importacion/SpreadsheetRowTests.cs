using Cobranzas_Vittoria.Domain.Importacion;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion;

/// <summary>
/// Pruebas unitarias de <see cref="SpreadsheetRow"/>.
/// Cubre accesores tipados, case-insensitive lookup y mensajes de error con numero de fila.
/// </summary>
public class SpreadsheetRowTests
{
    private static SpreadsheetRow CrearFila(int numeroFila, params (string col, string val)[] celdas)
    {
        var dict = celdas.ToDictionary(c => c.col, c => c.val);
        return new SpreadsheetRow(numeroFila, dict);
    }

    [Test]
    public void Constructor_NumeroFilaMenorA1_LanzaArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpreadsheetRow(0, new Dictionary<string, string>()));
    }

    [Test]
    public void Constructor_DiccionarioNulo_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SpreadsheetRow(1, null!));
    }

    [Test]
    public void GetString_ColumnaExistente_DevuelveValor()
    {
        var fila = CrearFila(3, ("Nombre", "Kilogramo"));
        Assert.That(fila.GetString("Nombre"), Is.EqualTo("Kilogramo"));
    }

    [Test]
    public void GetString_ColumnaInexistente_DevuelveNull()
    {
        var fila = CrearFila(1, ("Nombre", "X"));
        Assert.That(fila.GetString("NoExiste"), Is.Null);
    }

    [Test]
    public void GetString_ColumnaVacia_DevuelveNull()
    {
        var fila = CrearFila(1, ("Nombre", ""));
        Assert.That(fila.GetString("Nombre"), Is.Null);
    }

    [Test]
    public void ContieneColumna_BusquedaCaseInsensitive()
    {
        var fila = CrearFila(1, ("Codigo", "UM001"));
        Assert.That(fila.ContieneColumna("Codigo"), Is.True);
        Assert.That(fila.ContieneColumna("CODIGO"), Is.True);
        Assert.That(fila.ContieneColumna("codigo"), Is.True);
        Assert.That(fila.ContieneColumna("Otro"), Is.False);
    }

    [Test]
    public void GetString_BusquedaCaseInsensitive()
    {
        var fila = CrearFila(1, ("Codigo", "UM001"));
        Assert.That(fila.GetString("codigo"), Is.EqualTo("UM001"));
        Assert.That(fila.GetString("CODIGO"), Is.EqualTo("UM001"));
    }

    [Test]
    public void GetInt32_ValorValido_DevuelveEntero()
    {
        var fila = CrearFila(2, ("Cantidad", "42"));
        Assert.That(fila.GetInt32("Cantidad"), Is.EqualTo(42));
    }

    [Test]
    public void GetInt32_ValorInvalido_LanzaFormatExceptionConNumeroDeFila()
    {
        var fila = CrearFila(7, ("Cantidad", "abc"));
        var ex = Assert.Throws<FormatException>(() => fila.GetInt32("Cantidad"))!;
        Assert.That(ex.Message, Does.Contain("fila 7"));
    }

    [Test]
    public void GetInt32_ColumnaInexistente_LanzaKeyNotFound()
    {
        var fila = CrearFila(1);
        Assert.Throws<KeyNotFoundException>(() => fila.GetInt32("NoExiste"));
    }

    [Test]
    public void GetDecimal_ValorConPunto_DevuelveDecimal()
    {
        var fila = CrearFila(1, ("Monto", "1234.56"));
        Assert.That(fila.GetDecimal("Monto"), Is.EqualTo(1234.56m));
    }

    [Test]
    public void GetDecimal_UsaInvariantCulture_NoFallaConPuntoDecimal()
    {
        // En es-PE el separador es coma, pero los archivos son en-US por convencion.
        var fila = CrearFila(1, ("Monto", "99.99"));
        Assert.That(fila.GetDecimal("Monto"), Is.EqualTo(99.99m));
    }

    [Test]
    public void GetBool_AceptaValoresEspanolesEIngleses()
    {
        var f1 = CrearFila(1, ("Activo", "true"));
        var f2 = CrearFila(1, ("Activo", "false"));
        var f3 = CrearFila(1, ("Activo", "1"));
        var f4 = CrearFila(1, ("Activo", "0"));
        var f5 = CrearFila(1, ("Activo", "si"));
        var f6 = CrearFila(1, ("Activo", "no"));

        Assert.That(f1.GetBool("Activo"), Is.True);
        Assert.That(f2.GetBool("Activo"), Is.False);
        Assert.That(f3.GetBool("Activo"), Is.True);
        Assert.That(f4.GetBool("Activo"), Is.False);
        Assert.That(f5.GetBool("Activo"), Is.True);
        Assert.That(f6.GetBool("Activo"), Is.False);
    }

    [Test]
    public void GetBool_ValorInvalido_LanzaFormatException()
    {
        var fila = CrearFila(1, ("Activo", "talvez"));
        Assert.Throws<FormatException>(() => fila.GetBool("Activo"));
    }

    [Test]
    public void GetDateTime_FechaIso_DevuelveDateTime()
    {
        var fila = CrearFila(1, ("Fecha", "2026-07-14"));
        Assert.That(fila.GetDateTime("Fecha"), Is.EqualTo(new DateTime(2026, 7, 14)));
    }

    [Test]
    public void TryGetInt32_ValorInvalido_DevuelveFalse()
    {
        var fila = CrearFila(1, ("Cantidad", "abc"));
        Assert.That(fila.TryGetInt32("Cantidad", out _), Is.False);
    }

    [Test]
    public void TryGetInt32_ColumnaInexistente_DevuelveFalse()
    {
        var fila = CrearFila(1);
        Assert.That(fila.TryGetInt32("NoExiste", out _), Is.False);
    }
}
