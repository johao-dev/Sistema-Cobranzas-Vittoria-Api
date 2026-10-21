using System.Data;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Domain.Persistence;
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
    public Func<FiltroKardex, IReadOnlyList<KardexEntrada>>? OnListar { get; set; }
    public Func<RegistroKardexEntrada, KardexEntrada>? OnRegistrar { get; set; }
    public Func<RegistroKardexEntrada, KardexEntrada>? OnActualizar { get; set; }
    public Func<int, Task>? OnEliminar { get; set; }

    public List<FiltroKardex> LlamadasListar { get; } = new();
    public List<RegistroKardexEntrada> LlamadasRegistrar { get; } = new();
    public List<RegistroKardexEntrada> LlamadasActualizar { get; } = new();
    public List<int> LlamadasEliminar { get; } = new();

    public Task<IReadOnlyList<KardexEntrada>> ListarAsync(
        FiltroKardex filtro, CancellationToken ct = default)
    {
        filtro ??= new FiltroKardex();
        LlamadasListar.Add(filtro);
        var result = OnListar?.Invoke(filtro) ?? new List<KardexEntrada>();
        return Task.FromResult(result);
    }

    public Task<KardexEntrada> RegistrarAsync(
        RegistroKardexEntrada dto, CancellationToken ct = default)
    {
        LlamadasRegistrar.Add(dto);
        if (OnRegistrar is null) throw new InvalidOperationException("OnRegistrar no fue configurado.");
        return Task.FromResult(OnRegistrar(dto));
    }

    public Task<KardexEntrada> ActualizarAsync(
        RegistroKardexEntrada dto, CancellationToken ct = default)
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
    public Func<FiltroKardex, IReadOnlyList<KardexSalida>>? OnListar { get; set; }
    public Func<RegistroKardexSalida, IReadOnlyList<KardexSalida>>? OnRegistrar { get; set; }
    public Func<RegistroKardexSalida, IReadOnlyList<KardexSalida>>? OnActualizar { get; set; }
    public Func<int, Task>? OnEliminar { get; set; }

    public List<FiltroKardex> LlamadasListar { get; } = new();
    public List<RegistroKardexSalida> LlamadasRegistrar { get; } = new();
    public List<RegistroKardexSalida> LlamadasActualizar { get; } = new();
    public List<int> LlamadasEliminar { get; } = new();

    public Task<IReadOnlyList<KardexSalida>> ListarAsync(
        FiltroKardex filtro, CancellationToken ct = default)
    {
        filtro ??= new FiltroKardex();
        LlamadasListar.Add(filtro);
        var result = OnListar?.Invoke(filtro) ?? new List<KardexSalida>();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<KardexSalida>> RegistrarAsync(
        RegistroKardexSalida dto, CancellationToken ct = default)
    {
        LlamadasRegistrar.Add(dto);
        if (OnRegistrar is null) throw new InvalidOperationException("OnRegistrar no fue configurado.");
        return Task.FromResult(OnRegistrar(dto));
    }

    public Task<IReadOnlyList<KardexSalida>> ActualizarAsync(
        RegistroKardexSalida dto, CancellationToken ct = default)
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
    public Func<FiltroStockKardex, IReadOnlyList<StockKardex>>? OnListar { get; set; }
    public List<FiltroStockKardex> LlamadasListar { get; } = new();

    public Task<IReadOnlyList<StockKardex>> ListarAsync(
        FiltroStockKardex filtro, CancellationToken ct = default)
    {
        filtro ??= new FiltroStockKardex();
        LlamadasListar.Add(filtro);
        var result = OnListar?.Invoke(filtro) ?? new List<StockKardex>();
        return Task.FromResult(result);
    }
}
