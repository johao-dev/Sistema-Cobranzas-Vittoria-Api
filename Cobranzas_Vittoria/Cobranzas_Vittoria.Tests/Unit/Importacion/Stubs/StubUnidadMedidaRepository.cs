using System.Data;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Stubs;

/// <summary>
/// Stub de <see cref="IUnidadMedidaRepository"/> con colecciones mutables
/// para que cada test arme el escenario. Igual patron que
/// <see cref="StubEspecialidadRepository"/> pero usando el DTO
/// <see cref="UnidadMedidaDto"/> que requiere la interfaz.
/// </summary>
public sealed class StubUnidadMedidaRepository : IUnidadMedidaRepository
{
    public List<UnidadMedidaDto> Unidades { get; } = new();

    /// <summary>Bandera para forzar que <see cref="UpsertEnTransaccionAsync"/> lance una excepcion (ej: tests de retry de concurrencia).</summary>
    public Func<UnidadMedidaUpsertDto, Task<int>>? OnUpsertEnTransaccion { get; set; }

    public Task<IEnumerable<UnidadMedidaDto>> ListAsync(bool? activo)
    {
        IEnumerable<UnidadMedidaDto> q = Unidades;
        if (activo.HasValue) q = q.Where(u => u.Activo == activo.Value);
        return Task.FromResult(q.AsEnumerable());
    }

    public Task<IEnumerable<UnidadMedidaDto>> ListEnTransaccionAsync(
        bool? activo, IDbConnection cn, IDbTransaction? tx, CancellationToken ct)
        => ListAsync(activo);

    public Task<int> UpsertAsync(UnidadMedidaUpsertDto dto)
        => throw new NotImplementedException("Stub no soporta UpsertAsync.");

    public Task<int> UpsertEnTransaccionAsync(
        UnidadMedidaUpsertDto dto, IDbConnection cn, IDbTransaction tx, CancellationToken ct)
    {
        if (OnUpsertEnTransaccion is not null)
            return OnUpsertEnTransaccion(dto);

        // Default: simula la insercion sumando 1 al maximo Id y agregando a la lista.
        var nuevoId = Unidades.Count == 0 ? 1 : Unidades.Max(u => u.IdUnidadMedida) + 1;
        Unidades.Add(new UnidadMedidaDto
        {
            IdUnidadMedida = nuevoId,
            Codigo = dto.Codigo,
            Nombre = dto.Nombre,
            Activo = dto.Activo
        });
        return Task.FromResult(nuevoId);
    }

    public void Add(int idUnidadMedida, string codigo, string nombre, bool activo = true)
        => Unidades.Add(new UnidadMedidaDto
        {
            IdUnidadMedida = idUnidadMedida,
            Codigo = codigo,
            Nombre = nombre,
            Activo = activo
        });
}
