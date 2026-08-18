using Cobranzas_Vittoria.Application.Common;

namespace Cobranzas_Vittoria.Tests.Unit.Common;

/// <summary>
/// Pruebas unitarias de <see cref="SqlExceptionTranslator"/>.
///
/// El translator tiene dos responsabilidades:
///   1) Filtrar por rango (51100-51199). Fuera de rango, devuelve null.
///   2) Parsear el mensaje del SP en formato "CODIGO: detalle" y devolver
///      un <see cref="ResultadoTraduccionSql"/> inmutable.
///
/// Para testear la logica sin construir una SqlException real (que es
/// sealed y su propiedad Number no se puede inyectar de forma estable),
/// se usa el overload de testing <c>Traducir(int number, string message)</c>.
/// La logica de ambos overloads es la misma; el overload que toma
/// <c>SqlException</c> delega a este.
/// </summary>
public class SqlExceptionTranslatorUnitTests
{
    // =========================================================================
    // Filtrado por rango
    // =========================================================================

    [Test]
    public void Traducir_NumeroEnInicioDeRango_DevuelveResultadoConCodigo()
    {
        var result = SqlExceptionTranslator.Traducir(
            SqlExceptionTranslator.RangoInventarioInicio,
            "STOCK_INSUFICIENTE: El material X no tiene stock.");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.NumeroSql, Is.EqualTo(51100));
        Assert.That(result.CodigoError, Is.EqualTo("STOCK_INSUFICIENTE"));
        Assert.That(result.Mensaje, Is.EqualTo("El material X no tiene stock."));
    }

    [Test]
    public void Traducir_NumeroEnFinDeRango_DevuelveResultadoConCodigo()
    {
        var result = SqlExceptionTranslator.Traducir(
            SqlExceptionTranslator.RangoInventarioFin,
            "CODIGO_FIN_RANGO: detalle.");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.NumeroSql, Is.EqualTo(51199));
    }

    [Test]
    public void Traducir_NumeroEnMedioDeRango_DevuelveResultadoConCodigo()
    {
        var result = SqlExceptionTranslator.Traducir(51110, "STOCK_INSUFICIENTE: detalle.");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.NumeroSql, Is.EqualTo(51110));
    }

    [Test]
    public void Traducir_NumeroInferiorAlRango_DevuelveNull()
    {
        var result = SqlExceptionTranslator.Traducir(51099, "COMPRAS: detalle.");

        Assert.That(result, Is.Null,
            "51099 pertenece al modulo de Compras, no a Inventario.");
    }

    [Test]
    public void Traducir_NumeroSuperiorAlRango_DevuelveNull()
    {
        var result = SqlExceptionTranslator.Traducir(51200, "OTRO: detalle.");

        Assert.That(result, Is.Null,
            "51200 esta fuera del rango reservado (51100-51199).");
    }

    [Test]
    public void Traducir_NumeroCero_DevuelveNull()
    {
        var result = SqlExceptionTranslator.Traducir(0, string.Empty);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Traducir_NumeroNegativo_DevuelveNull()
    {
        var result = SqlExceptionTranslator.Traducir(-1, "x");
        Assert.That(result, Is.Null);
    }

    // =========================================================================
    // Parseo del mensaje
    // =========================================================================

    [Test]
    public void Traducir_MensajeConPrefijoYDetalle_DevuelveCodigoYDetalleSeparados()
    {
        var result = SqlExceptionTranslator.Traducir(51110, "STOCK_INSUFICIENTE: El stock actual es 0.");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodigoError, Is.EqualTo("STOCK_INSUFICIENTE"));
        Assert.That(result.Mensaje, Is.EqualTo("El stock actual es 0."));
    }

    [Test]
    public void Traducir_MensajeConEspaciosAlrededorDeLosDosPuntos_Limpia()
    {
        // El SP emite con o sin espacios; el parser debe tolerarlo.
        var result = SqlExceptionTranslator.Traducir(51104, "  KARDEX_NO_ENCONTRADO :   detalle con espacios   ");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodigoError, Is.EqualTo("KARDEX_NO_ENCONTRADO"));
        Assert.That(result.Mensaje, Is.EqualTo("detalle con espacios"));
    }

    [Test]
    public void Traducir_MensajeSinSeparador_UsaCodigoGenerico()
    {
        var result = SqlExceptionTranslator.Traducir(51110, "El stock es negativo");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodigoError, Is.EqualTo("ERROR_VALIDACION"));
        Assert.That(result.Mensaje, Is.EqualTo("El stock es negativo"));
    }

    [Test]
    public void Traducir_MensajeVacio_UsaCodigoErrorSqlVacio()
    {
        var result = SqlExceptionTranslator.Traducir(51110, string.Empty);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodigoError, Is.EqualTo("ERROR_SQL_VACIO"));
        Assert.That(result.Mensaje, Does.Contain("no devolvio mensaje"));
    }

    [Test]
    public void Traducir_MensajeConSoloDosPuntosAlInicio_UsaCodigoGenerico()
    {
        // ":detalle" -> prefijo vacio -> codigo generico.
        var result = SqlExceptionTranslator.Traducir(51110, ":detalle sin prefijo");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodigoError, Is.EqualTo("ERROR_VALIDACION"));
        Assert.That(result.Mensaje, Is.EqualTo(":detalle sin prefijo"));
    }

    [Test]
    public void Traducir_MensajeConMultiplesDosPuntos_DividePorElPrimero()
    {
        // "CODIGO: parte1: parte2" -> codigo = "CODIGO", detalle = "parte1: parte2".
        var result = SqlExceptionTranslator.Traducir(51110, "CODIGO: parte1: parte2");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodigoError, Is.EqualTo("CODIGO"));
        Assert.That(result.Mensaje, Is.EqualTo("parte1: parte2"));
    }

    [Test]
    public void Traducir_FilaEsCero_PorConvencion()
    {
        // Los SPs de Inventario no reportan fila. Se deja como 0 en el resultado
        // para que el caller (KardexInventarioService) pueda ignorarlo sin
        // romper el contrato.
        var result = SqlExceptionTranslator.Traducir(51110, "CODIGO: detalle");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Fila, Is.EqualTo(0));
    }

    // =========================================================================
    // Constantes de rango (regresion: el rango no debe cambiar sin avisar)
    // =========================================================================

    [Test]
    public void RangoInventario_ValoresEsperados_Regresion()
    {
        Assert.That(SqlExceptionTranslator.RangoInventarioInicio, Is.EqualTo(51100));
        Assert.That(SqlExceptionTranslator.RangoInventarioFin, Is.EqualTo(51199));
    }
}
