using System.Data;
using Cobranzas_Vittoria.Application.Inventario.Dtos;
using Cobranzas_Vittoria.Application.Inventario.Persistence;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;

/// <summary>
/// Stubs manuales de los repositorios legacy de Maestra que consume
/// <c>KardexInventarioValidator</c>. El proyecto no usa Moq ni NSubstitute
/// (no estan en <c>Cobranzas_Vittoria.Tests.csproj</c>); en su lugar seguimos
/// la convencion de <c>ImportServiceUnitTests</c> con stubs in-memory.
///
/// Cada stub es mutable en sus colecciones para que el test arme el
/// escenario. La mayoria de los metodos no usados por el validator
/// (UpsertAsync, etc.) lanzan <c>NotImplementedException</c> para
/// atrapar errores si se llaman accidentalmente.
/// </summary>
public sealed class StubEspecialidadRepository : IEspecialidadRepository
{
    public List<Especialidad> Especialidades { get; } = new();

    /// <summary>Bandera para forzar que <see cref="UpsertEnTransaccionAsync"/> lance una excepcion (ej: tests de retry de concurrencia).</summary>
    public Func<EspecialidadUpsertDto, Task<int>>? OnUpsertEnTransaccion { get; set; }

    public Task<IEnumerable<Especialidad>> ListAsync(bool? activo)
    {
        IEnumerable<Especialidad> q = Especialidades;
        if (activo.HasValue) q = q.Where(e => e.Activo == activo.Value);
        return Task.FromResult(q.AsEnumerable());
    }

    public Task<IEnumerable<Especialidad>> ListEnTransaccionAsync(
        bool? activo, IDbConnection cn, IDbTransaction? tx, CancellationToken ct)
    {
        // Reutiliza la logica de ListAsync; el test no usa realmente la transaccion
        // porque opera sobre la coleccion in-memory.
        return ListAsync(activo);
    }

    public Task<int> UpsertAsync(EspecialidadUpsertDto dto)
        => throw new NotImplementedException("Stub no soporta UpsertAsync.");

    public Task<int> UpsertEnTransaccionAsync(
        EspecialidadUpsertDto dto, IDbConnection cn, IDbTransaction tx, CancellationToken ct)
    {
        if (OnUpsertEnTransaccion is not null)
            return OnUpsertEnTransaccion(dto);

        // Default: simula la insercion sumando 1 al maximo Id y agregando a la lista.
        var nuevoId = Especialidades.Count == 0 ? 1 : Especialidades.Max(e => e.IdEspecialidad) + 1;
        Especialidades.Add(new Especialidad
        {
            IdEspecialidad = nuevoId,
            Nombre = dto.Nombre,
            Activo = dto.Activo
        });
        return Task.FromResult(nuevoId);
    }

    public void Add(int idEspecialidad, string nombre, bool activo = true)
        => Especialidades.Add(new Especialidad
        {
            IdEspecialidad = idEspecialidad,
            Nombre = nombre,
            Activo = activo
        });
}

public sealed class StubMaterialRepository : IMaterialRepository
{
    public List<Material> Materiales { get; } = new();

    public Task<IEnumerable<Material>> ListAsync(bool? activo, int? idEspecialidad)
    {
        IEnumerable<Material> q = Materiales;
        if (activo.HasValue) q = q.Where(m => m.Activo == activo.Value);
        if (idEspecialidad.HasValue) q = q.Where(m => m.IdEspecialidad == idEspecialidad.Value);
        return Task.FromResult(q.AsEnumerable());
    }

    public Task<Material?> GetAsync(int idMaterial)
        => Task.FromResult(Materiales.FirstOrDefault(m => m.IdMaterial == idMaterial));

    public Task<string> GetSiguienteCodigoAsync()
        => throw new NotImplementedException("Stub no soporta GetSiguienteCodigoAsync.");

    public Task<int> UpsertAsync(MaterialUpsertDto dto)
        => throw new NotImplementedException("Stub no soporta UpsertAsync.");

    public void Add(int idMaterial, int idEspecialidad, string descripcion, bool activo = true)
        => Materiales.Add(new Material
        {
            IdMaterial = idMaterial,
            IdEspecialidad = idEspecialidad,
            Descripcion = descripcion,
            UnidadMedida = "UND",
            Activo = activo
        });
}

public sealed class StubProveedorRepository : IProveedorRepository
{
    public List<Proveedor> Proveedores { get; } = new();

    public Task<IEnumerable<Proveedor>> ListAsync(bool? activo, int? idEspecialidad)
    {
        IEnumerable<Proveedor> q = Proveedores;
        if (activo.HasValue) q = q.Where(p => p.Activo == activo.Value);
        if (idEspecialidad.HasValue)
        {
            // Stub minimal: filtra por proveedor que tenga la especialidad en su lista.
            // Para el validator esto es suficiente porque usa GetAsync, no ListAsync.
            q = q.Where(p => p.RazonSocial.Contains(idEspecialidad.Value.ToString()));
        }
        return Task.FromResult(q.AsEnumerable());
    }

    public Task<(Proveedor? proveedor, List<ProveedorEspecialidad> especialidades)> GetAsync(int idProveedor)
    {
        var p = Proveedores.FirstOrDefault(x => x.IdProveedor == idProveedor);
        return Task.FromResult((p, new List<ProveedorEspecialidad>()));
    }

    public Task<int> UpsertAsync(ProveedorUpsertDto dto)
        => throw new NotImplementedException("Stub no soporta UpsertAsync.");

    public Task SetEspecialidadAsync(int idProveedor, int idEspecialidad, bool activo)
        => throw new NotImplementedException("Stub no soporta SetEspecialidadAsync.");

    public void Add(int idProveedor, string razonSocial, bool activo = true)
        => Proveedores.Add(new Proveedor
        {
            IdProveedor = idProveedor,
            RazonSocial = razonSocial,
            Ruc = "20000000000",
            Activo = activo
        });
}

public sealed class StubProyectoRepository : IProyectoRepository
{
    public List<Proyecto> Proyectos { get; } = new();

    public Task<IEnumerable<Proyecto>> ListAsync(bool? activo)
    {
        IEnumerable<Proyecto> q = Proyectos;
        if (activo.HasValue) q = q.Where(p => p.Activo == activo.Value);
        return Task.FromResult(q.AsEnumerable());
    }

    public Task<int> UpsertAsync(ProyectoUpsertDto dto)
        => throw new NotImplementedException("Stub no soporta UpsertAsync.");

    public void Add(int idProyecto, string nombre, bool activo = true)
        => Proyectos.Add(new Proyecto
        {
            IdProyecto = idProyecto,
            NombreProyecto = nombre,
            Activo = activo
        });
}

// =============================================================================
// Stubs de los repositorios del modulo Inventario que consume KardexInventarioService
// =============================================================================

/// <summary>
/// Stub de <see cref="IKardexEntradaRepository"/> con callbacks configurables
/// para que cada test defina la respuesta (o excepcion) de cada operacion.
/// </summary>
public sealed class StubKardexEntradaRepository : IKardexEntradaRepository
{
    public Func<KardexFiltroInventarioDto, IReadOnlyList<KardexEntradaResponseDto>>? OnListar { get; set; }
    public Func<KardexEntradaCreateDto, KardexEntradaResponseDto>? OnRegistrar { get; set; }
    public Func<KardexEntradaCreateDto, KardexEntradaResponseDto>? OnActualizar { get; set; }
    public Func<int, Task>? OnEliminar { get; set; }

    public List<KardexFiltroInventarioDto> LlamadasListar { get; } = new();
    public List<KardexEntradaCreateDto> LlamadasRegistrar { get; } = new();
    public List<KardexEntradaCreateDto> LlamadasActualizar { get; } = new();
    public List<int> LlamadasEliminar { get; } = new();

    public Task<IReadOnlyList<KardexEntradaResponseDto>> ListarAsync(
        KardexFiltroInventarioDto filtro, CancellationToken ct = default)
    {
        filtro ??= new KardexFiltroInventarioDto();
        LlamadasListar.Add(filtro);
        var result = OnListar?.Invoke(filtro) ?? new List<KardexEntradaResponseDto>();
        return Task.FromResult(result);
    }

    public Task<KardexEntradaResponseDto> RegistrarAsync(
        KardexEntradaCreateDto dto, CancellationToken ct = default)
    {
        LlamadasRegistrar.Add(dto);
        if (OnRegistrar is null) throw new InvalidOperationException("OnRegistrar no fue configurado.");
        return Task.FromResult(OnRegistrar(dto));
    }

    public Task<KardexEntradaResponseDto> ActualizarAsync(
        KardexEntradaCreateDto dto, CancellationToken ct = default)
    {
        LlamadasActualizar.Add(dto);
        if (OnActualizar is null) throw new InvalidOperationException("OnActualizar no fue configurado.");
        return Task.FromResult(OnActualizar(dto));
    }

    public Task EliminarAsync(int idKardexEntrada, CancellationToken ct = default)
    {
        LlamadasEliminar.Add(idKardexEntrada);
        if (OnEliminar is null) throw new InvalidOperationException("OnEliminar no fue configurado.");
        return OnEliminar(idKardexEntrada);
    }
}

public sealed class StubKardexSalidaRepository : IKardexSalidaRepository
{
    public Func<KardexFiltroInventarioDto, IReadOnlyList<KardexSalidaResponseDto>>? OnListar { get; set; }
    public Func<KardexSalidaCreateDto, IReadOnlyList<KardexSalidaResponseDto>>? OnRegistrar { get; set; }
    public Func<KardexSalidaCreateDto, IReadOnlyList<KardexSalidaResponseDto>>? OnActualizar { get; set; }
    public Func<int, Task>? OnEliminar { get; set; }

    public List<KardexFiltroInventarioDto> LlamadasListar { get; } = new();
    public List<KardexSalidaCreateDto> LlamadasRegistrar { get; } = new();
    public List<KardexSalidaCreateDto> LlamadasActualizar { get; } = new();
    public List<int> LlamadasEliminar { get; } = new();

    public Task<IReadOnlyList<KardexSalidaResponseDto>> ListarAsync(
        KardexFiltroInventarioDto filtro, CancellationToken ct = default)
    {
        filtro ??= new KardexFiltroInventarioDto();
        LlamadasListar.Add(filtro);
        var result = OnListar?.Invoke(filtro) ?? new List<KardexSalidaResponseDto>();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<KardexSalidaResponseDto>> RegistrarAsync(
        KardexSalidaCreateDto dto, CancellationToken ct = default)
    {
        LlamadasRegistrar.Add(dto);
        if (OnRegistrar is null) throw new InvalidOperationException("OnRegistrar no fue configurado.");
        return Task.FromResult(OnRegistrar(dto));
    }

    public Task<IReadOnlyList<KardexSalidaResponseDto>> ActualizarAsync(
        KardexSalidaCreateDto dto, CancellationToken ct = default)
    {
        LlamadasActualizar.Add(dto);
        if (OnActualizar is null) throw new InvalidOperationException("OnActualizar no fue configurado.");
        return Task.FromResult(OnActualizar(dto));
    }

    public Task EliminarAsync(int idKardexSalida, CancellationToken ct = default)
    {
        LlamadasEliminar.Add(idKardexSalida);
        if (OnEliminar is null) throw new InvalidOperationException("OnEliminar no fue configurado.");
        return OnEliminar(idKardexSalida);
    }
}

public sealed class StubKardexStockRepository : IKardexStockRepository
{
    public Func<KardexStockFiltroInventarioDto, IReadOnlyList<KardexStockActualResponseDto>>? OnListar { get; set; }
    public List<KardexStockFiltroInventarioDto> LlamadasListar { get; } = new();

    public Task<IReadOnlyList<KardexStockActualResponseDto>> ListarAsync(
        KardexStockFiltroInventarioDto filtro, CancellationToken ct = default)
    {
        filtro ??= new KardexStockFiltroInventarioDto();
        LlamadasListar.Add(filtro);
        var result = OnListar?.Invoke(filtro) ?? new List<KardexStockActualResponseDto>();
        return Task.FromResult(result);
    }
}
