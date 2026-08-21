using System.Data;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Stubs;

/// <summary>
/// Stub de <see cref="ResolvedorEntidadesService"/> para tests unitarios del
/// <c>MaterialImportProcessor</c>.
///
/// <para>
/// En lugar de resolver entidades contra la BD real, devuelve IDs fijos
/// preestablecidos (o asignados secuencialmente si no se configuran).
/// Esto permite instanciar el processor y ejercitar <c>MapearFila</c> sin
/// necesitar un SQL Server levantado.
/// </para>
///
/// <para>
/// Hereda de la clase real (no implementa una interfaz) para que el
/// processor la consuma de forma transparente. Los metodos publicos son
/// <c>virtual</c> en la clase base, asi que el override aqui es directo.
/// </para>
/// </summary>
public sealed class StubResolvedorEntidadesService : ResolvedorEntidadesService
{
    private readonly Dictionary<string, int> _especialidades = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _unidades = new(StringComparer.OrdinalIgnoreCase);
    private int _nextEspecialidadId = 1000;
    private int _nextUnidadId = 2000;

    /// <summary>Si se establece, se lanza esta excepcion al resolver una Especialidad.</summary>
    public Func<string, Exception>? OnEspecialidadError { get; set; }

    /// <summary>Si se establece, se lanza esta excepcion al resolver una UnidadMedida.</summary>
    public Func<string, Exception>? OnUnidadError { get; set; }

    public StubResolvedorEntidadesService()
        : base(
            especialidadRepo: new NullEspecialidadRepository(),
            unidadMedidaRepo: new NullUnidadMedidaRepository(),
            logger: NullLogger<ResolvedorEntidadesService>.Instance)
    {
    }

    /// <summary>Registra un mapeo nombre -> id que el stub devolvera.</summary>
    public void AddEspecialidad(string nombre, int id) => _especialidades[nombre] = id;

    /// <summary>Registra un mapeo nombre -> id que el stub devolvera.</summary>
    public void AddUnidadMedida(string nombre, int id) => _unidades[nombre] = id;

    public override Task<int> ResolverIdEspecialidadAsync(
        string nombre, IDbConnection cn, IDbTransaction tx, CancellationToken ct)
    {
        if (OnEspecialidadError is not null)
            throw OnEspecialidadError(nombre);

        if (_especialidades.TryGetValue(nombre, out var id))
            return Task.FromResult(id);

        // Fallback: asigna un id sintetico y lo registra para las llamadas siguientes.
        id = _nextEspecialidadId++;
        _especialidades[nombre] = id;
        return Task.FromResult(id);
    }

    public override Task<int> ResolverIdUnidadMedidaAsync(
        string nombre, IDbConnection cn, IDbTransaction tx, CancellationToken ct)
    {
        if (OnUnidadError is not null)
            throw OnUnidadError(nombre);

        if (_unidades.TryGetValue(nombre, out var id))
            return Task.FromResult(id);

        id = _nextUnidadId++;
        _unidades[nombre] = id;
        return Task.FromResult(id);
    }

    /// <summary>Repositorio de Especialidad que no se usa (el stub override los metodos); existe solo para cumplir el constructor.</summary>
    private sealed class NullEspecialidadRepository : IEspecialidadRepository
    {
        public Task<IEnumerable<Cobranzas_Vittoria.Entities.Especialidad>> ListAsync(bool? activo)
            => throw new NotSupportedException("Stub no usa el repositorio real.");
        public Task<IEnumerable<Cobranzas_Vittoria.Entities.Especialidad>> ListEnTransaccionAsync(
            bool? activo, IDbConnection cn, IDbTransaction? tx, CancellationToken ct)
            => throw new NotSupportedException("Stub no usa el repositorio real.");
        public Task<int> UpsertAsync(Cobranzas_Vittoria.Dtos.Maestra.EspecialidadUpsertDto dto)
            => throw new NotSupportedException("Stub no usa el repositorio real.");
        public Task<int> UpsertEnTransaccionAsync(
            Cobranzas_Vittoria.Dtos.Maestra.EspecialidadUpsertDto dto,
            IDbConnection cn, IDbTransaction tx, CancellationToken ct)
            => throw new NotSupportedException("Stub no usa el repositorio real.");
    }

    /// <summary>Repositorio de UnidadMedida que no se usa; existe solo para cumplir el constructor.</summary>
    private sealed class NullUnidadMedidaRepository : IUnidadMedidaRepository
    {
        public Task<IEnumerable<Cobranzas_Vittoria.Dtos.Maestra.UnidadMedidaDto>> ListAsync(bool? activo)
            => throw new NotSupportedException("Stub no usa el repositorio real.");
        public Task<IEnumerable<Cobranzas_Vittoria.Dtos.Maestra.UnidadMedidaDto>> ListEnTransaccionAsync(
            bool? activo, IDbConnection cn, IDbTransaction? tx, CancellationToken ct)
            => throw new NotSupportedException("Stub no usa el repositorio real.");
        public Task<int> UpsertAsync(Cobranzas_Vittoria.Dtos.Maestra.UnidadMedidaUpsertDto dto)
            => throw new NotSupportedException("Stub no usa el repositorio real.");
        public Task<int> UpsertEnTransaccionAsync(
            Cobranzas_Vittoria.Dtos.Maestra.UnidadMedidaUpsertDto dto,
            IDbConnection cn, IDbTransaction tx, CancellationToken ct)
            => throw new NotSupportedException("Stub no usa el repositorio real.");
    }
}
