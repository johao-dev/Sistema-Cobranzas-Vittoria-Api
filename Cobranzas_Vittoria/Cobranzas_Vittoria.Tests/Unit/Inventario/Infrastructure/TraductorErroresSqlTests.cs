using Cobranzas_Vittoria.Inventario.Application.Excepciones;
using Cobranzas_Vittoria.Inventario.Domain.Excepciones;
using Cobranzas_Vittoria.Inventario.Infrastructure.Persistence;
using Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Unit.Inventario.Infrastructure;

public class TraductorErroresSqlTests
{
    [TestCase(51110, "STOCK_INSUFICIENTE: disponible=0 solicitado=5", "STOCK_INSUFICIENTE", "disponible=0 solicitado=5")]
    [TestCase(51111, "STOCK_INCONSISTENTE_AL_ELIMINAR: Hay salidas posteriores.", "STOCK_INCONSISTENTE_AL_ELIMINAR", "Hay salidas posteriores.")]
    [TestCase(51110, "Error sin prefijo", "ERROR_VALIDACION", "Error sin prefijo")]
    public void EjecutarAsync_RechazoSql_ConservaCodigoYDetalle(int numero, string mensaje, string codigo, string detalle)
    {
        SqlException error = SqlExceptionBuilder.Crear(numero, mensaje);

        ValidacionNegocioInventarioException? resultado = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(
            () => TraductorErroresSql.EjecutarAsync<int>(() => Task.FromException<int>(error), "entrada"));

        Assert.That(resultado!.Errores, Has.Count.EqualTo(1));
        Assert.That(resultado.Errores[0].CodigoError, Is.EqualTo(codigo));
        Assert.That(resultado.Errores[0].Mensaje, Is.EqualTo(detalle));
    }

    [TestCase("entrada")]
    [TestCase("salida")]
    public void EjecutarAsync_KardexNoEncontrado_ConservaTipoDeMovimiento(string tipoKardex)
    {
        SqlException error = SqlExceptionBuilder.Crear(51104, "KARDEX_NO_ENCONTRADO: No existe el kardex.");

        KardexNoEncontradoException? resultado = Assert.ThrowsAsync<KardexNoEncontradoException>(
            () => TraductorErroresSql.EjecutarAsync(() => Task.FromException(error), tipoKardex));

        Assert.That(resultado!.TipoKardex, Is.EqualTo(tipoKardex));
    }

    [TestCase(51099)]
    [TestCase(51200)]
    public void EjecutarAsync_SqlAjenoAlModulo_PropagaLaMismaExcepcion(int numero)
    {
        SqlException error = SqlExceptionBuilder.Crear(numero, "Error SQL ajeno a Inventario.");

        SqlException? resultado = Assert.ThrowsAsync<SqlException>(
            () => TraductorErroresSql.EjecutarAsync(() => Task.FromException(error), "salida"));

        Assert.That(resultado, Is.SameAs(error));
    }

    [Test]
    public void EjecutarAsync_Cancelacion_PropagaLaMismaExcepcion()
    {
        OperationCanceledException error = new();

        OperationCanceledException? resultado = Assert.ThrowsAsync<OperationCanceledException>(
            () => TraductorErroresSql.EjecutarAsync(() => Task.FromException(error), "entrada"));

        Assert.That(resultado, Is.SameAs(error));
    }
}
