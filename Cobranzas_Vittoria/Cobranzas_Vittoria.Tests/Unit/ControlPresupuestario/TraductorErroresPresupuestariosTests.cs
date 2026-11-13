using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence;
using Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Unit.ControlPresupuestario;

public class TraductorErroresPresupuestariosTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Interbloqueo_SeTraduceAConflictoSinReintentosNiMensajeDelProveedor(bool conResultado)
    {
        SqlException error = SqlExceptionBuilder.Crear(1205, "Proceso SQL e información interna del proveedor");
        int intentos = 0;
        Task<int> Fallar()
        {
            intentos++;
            return Task.FromException<int>(error);
        }

        ControlPresupuestarioException? resultado = Assert.ThrowsAsync<ControlPresupuestarioException>(async () =>
        {
            if (conResultado)
            {
                await TraductorErroresSql.EjecutarAsync<int>(Fallar);
            }
            else
            {
                await TraductorErroresSql.EjecutarAsync(() => (Task)Fallar());
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(intentos, Is.EqualTo(1));
            Assert.That(resultado!.CodigoError, Is.EqualTo("CONFLICTO_CONCURRENCIA"));
            Assert.That(resultado.Message, Does.Not.Contain("información interna"));
        });
    }

    [Test]
    public void ErrorSqlAjeno_PropagaLaMismaExcepcion()
    {
        SqlException error = SqlExceptionBuilder.Crear(2627, "Error SQL ajeno al contrato presupuestario");

        SqlException? resultado = Assert.ThrowsAsync<SqlException>(
            () => TraductorErroresSql.EjecutarAsync<int>(() => Task.FromException<int>(error)));

        Assert.That(resultado, Is.SameAs(error));
    }
}
