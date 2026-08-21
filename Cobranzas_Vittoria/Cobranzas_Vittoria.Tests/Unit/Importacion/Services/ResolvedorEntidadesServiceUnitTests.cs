using System.Data;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Stubs;
using Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Services;

/// <summary>
/// Tests del <see cref="ResolvedorEntidadesService"/>. Cubren:
///   - Lookup de Especialidad/UnidadMedida existentes en el catalogo.
///   - Auto-creacion cuando el nombre no existe.
///   - Retry ante SqlException 2627 (UNIQUE violation por concurrencia).
///   - Validacion de argumentos (nombre vacio).
///
/// Usa los stubs <see cref="StubEspecialidadRepository"/> y
/// <see cref="StubUnidadMedidaRepository"/>, que son colecciones in-memory.
/// Los tests NO usan una conexion real; pasan un <see cref="SqlConnection"/>
/// vacio (no se abre) porque el resolvedor requiere una conexion no-null,
/// aunque los stubs la ignoren.
/// </summary>
public class ResolvedorEntidadesServiceUnitTests
{
    private readonly IDbConnection _cn = new SqlConnection();
    private StubEspecialidadRepository _especialidadRepo = null!;
    private StubUnidadMedidaRepository _unidadRepo = null!;
    private ResolvedorEntidadesService _service = null!;

    [SetUp]
    public void ResetearStubs()
    {
        // NUnit crea UNA instancia del fixture por defecto y la reusa en todos
        // los tests, lo que mantiene el estado de los stubs entre tests. Los
        // repositorios acumulan entidades; por eso los recreamos antes de cada
        // test para que cada uno arranque con un catalogo vacio.
        _especialidadRepo = new StubEspecialidadRepository();
        _unidadRepo = new StubUnidadMedidaRepository();
        _service = new ResolvedorEntidadesService(
            _especialidadRepo,
            _unidadRepo,
            NullLogger<ResolvedorEntidadesService>.Instance);
    }

    // =========================================================================
    // ResolverIdEspecialidadAsync
    // =========================================================================

    [Test]
    public async Task ResolverIdEspecialidad_ExisteEnCatalogo_DevuelveIdExistente_SinCrear()
    {
        _especialidadRepo.Add(42, "Albañileria", activo: true);

        var id = await _service.ResolverIdEspecialidadAsync("Albañileria", _cn, tx: null, ct: default);

        Assert.That(id, Is.EqualTo(42));
        Assert.That(_especialidadRepo.Especialidades.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task ResolverIdEspecialidad_NoExiste_CreaNuevaYDevuelveNuevoId()
    {
        // No hay especialidades precargadas. El resolver debe crear una.
        var id = await _service.ResolverIdEspecialidadAsync("Carpinteria", _cn, tx: null, ct: default);

        Assert.That(id, Is.GreaterThan(0));
        Assert.That(_especialidadRepo.Especialidades.Count, Is.EqualTo(1));
        Assert.That(_especialidadRepo.Especialidades[0].Nombre, Is.EqualTo("Carpinteria"));
    }

    [Test]
    public async Task ResolverIdEspecialidad_LookupCaseYAccentInsensitive()
    {
        // "ALBAÑILERIA" debe matchear con "Albañileria" registrada.
        _especialidadRepo.Add(7, "Albañileria", activo: true);

        var id1 = await _service.ResolverIdEspecialidadAsync("ALBAÑILERIA", _cn, tx: null, ct: default);
        var id2 = await _service.ResolverIdEspecialidadAsync("albanileria", _cn, tx: null, ct: default);

        Assert.That(id1, Is.EqualTo(7));
        Assert.That(id2, Is.EqualTo(7));
    }

    [Test]
    public void ResolverIdEspecialidad_NombreVacio_LanzaArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.ResolverIdEspecialidadAsync("   ", _cn, tx: null, ct: default));
    }

    [Test]
    public async Task ResolverIdEspecialidad_ChocaConUnique_ReintentaYEncuentraCreadaPorOtro()
    {
        // Primer intento: la especialidad no existe. La creacion choca con UNIQUE
        // (simulamos que otra transaccion la creo entre el SELECT y el INSERT).
        // El resolver debe reintentar: en el segundo intento, el catalogo ya tiene
        // la fila (porque el stub la agrega manualmente tras el primer fallo).
        _especialidadRepo.OnUpsertEnTransaccion = dto =>
        {
            // Simulamos que OTRA transaccion la inserto entre nuestro SELECT y nuestro INSERT.
            if (_especialidadRepo.Especialidades.Count == 0)
            {
                _especialidadRepo.Add(99, dto.Nombre);
                // 2627 = UNIQUE violation
                throw SqlExceptionBuilder.Crear(2627, "Violation of UNIQUE KEY constraint");
            }
            return Task.FromResult(100);
        };

        var id = await _service.ResolverIdEspecialidadAsync("Mecanica", _cn, tx: null, ct: default);

        // Debe devolver el id que la "otra transaccion" asigno.
        Assert.That(id, Is.EqualTo(99));
    }

    // =========================================================================
    // ResolverIdUnidadMedidaAsync
    // =========================================================================

    [Test]
    public async Task ResolverIdUnidadMedida_ExisteEnCatalogo_DevuelveIdExistente_SinCrear()
    {
        _unidadRepo.Add(5, "UM-MTR-0001", "Metro", activo: true);

        var id = await _service.ResolverIdUnidadMedidaAsync("Metro", _cn, tx: null, ct: default);

        Assert.That(id, Is.EqualTo(5));
        Assert.That(_unidadRepo.Unidades.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task ResolverIdUnidadMedida_NoExiste_CreaNuevaConCodigoAutogenerado()
    {
        var id = await _service.ResolverIdUnidadMedidaAsync("Kilogramo", _cn, tx: null, ct: default);

        Assert.That(id, Is.GreaterThan(0));
        Assert.That(_unidadRepo.Unidades.Count, Is.EqualTo(1));
        // Codigo autogenerado: UM-<SIGLA>-#### donde SIGLA = "KLG" (K,i,l,o,g,r,a,m,o)
        Assert.That(_unidadRepo.Unidades[0].Codigo, Does.StartWith("UM-"));
        Assert.That(_unidadRepo.Unidades[0].Codigo, Does.EndWith("-0001"));
        Assert.That(_unidadRepo.Unidades[0].Nombre, Is.EqualTo("Kilogramo"));
    }

    [Test]
    public void ResolverIdUnidadMedida_NombreVacio_LanzaArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.ResolverIdUnidadMedidaAsync("", _cn, tx: null, ct: default));
    }
}
