using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Compras;

namespace Cobranzas_Vittoria.Tests.Integration.Common;

/// <summary>
/// Fluent builder para construir y crear <see cref="RequerimientoCreateDto"/>.
/// Reutilizable entre controllers que necesitan un requerimiento previo
/// (OrdenesCompra, Compras, Kardex, etc.).
///
/// Defaults:
///   - NumeroRequerimiento = Guid(8 chars) → único por test
///   - FechaRequerimiento = DateTime.Today
///   - IdEspecialidad = SeedIds.EspecialidadAlbanileria
///   - IdProyecto = SeedIds.ProyectoMaytaCapacII
///   - IdUsuarioSolicitante = SeedIds.IngenieroId
///   - Items = 1 item con material 2 (MORTERO LISTO) y cantidad 10
/// </summary>
public class RequerimientoBuilder
{
    private readonly RequerimientoCreateDto _dto = new();

    public static RequerimientoBuilder Nuevo() => new();

    public RequerimientoBuilder ConNumero(string numero)
    {
        _dto.NumeroRequerimiento = numero;
        return this;
    }

    public RequerimientoBuilder ConFechaRequerimiento(DateTime fecha)
    {
        _dto.FechaRequerimiento = fecha;
        return this;
    }

    public RequerimientoBuilder ConIdEspecialidad(int id)
    {
        _dto.IdEspecialidad = id;
        return this;
    }

    public RequerimientoBuilder ConIdProyecto(int id)
    {
        _dto.IdProyecto = id;
        return this;
    }

    public RequerimientoBuilder ConIdUsuarioSolicitante(int id)
    {
        _dto.IdUsuarioSolicitante = id;
        return this;
    }

    public RequerimientoBuilder ConDescripcion(string descripcion)
    {
        _dto.Descripcion = descripcion;
        return this;
    }

    public RequerimientoBuilder ConObservacion(string observacion)
    {
        _dto.Observacion = observacion;
        return this;
    }

    public RequerimientoBuilder ConFechaEntrega(DateTime fecha)
    {
        _dto.FechaEntrega = fecha;
        return this;
    }

    public RequerimientoBuilder ConItem(int idMaterial, decimal cantidad, string? observacion = null)
    {
        _dto.Items.Add(new RequerimientoDetalleCreateDto
        {
            IdMaterial = idMaterial,
            Cantidad = cantidad,
            Observacion = observacion
        });
        return this;
    }

    /// <summary>
    /// Aplica los defaults si los campos no fueron configurados, y retorna el DTO.
    /// NO ejecuta el POST.
    /// </summary>
    public RequerimientoCreateDto Build()
    {
        if (string.IsNullOrEmpty(_dto.NumeroRequerimiento))
            _dto.NumeroRequerimiento = Guid.NewGuid().ToString("N").Substring(0, 8);
        if (_dto.FechaRequerimiento == default)
            _dto.FechaRequerimiento = DateTime.Today;
        if (_dto.IdEspecialidad == 0)
            _dto.IdEspecialidad = SeedIds.EspecialidadAlbanileria;
        if (_dto.IdProyecto == 0)
            _dto.IdProyecto = SeedIds.ProyectoMaytaCapacII;
        if (_dto.IdUsuarioSolicitante == 0)
            _dto.IdUsuarioSolicitante = SeedIds.IngenieroId;
        if (_dto.Items.Count == 0)
            _dto.Items.Add(new RequerimientoDetalleCreateDto
            {
                IdMaterial = 2,
                Cantidad = 10m,
                Observacion = "Item por defecto"
            });
        return _dto;
    }

    /// <summary>
    /// Construye el DTO y ejecuta POST /api/compras/requerimientos.
    /// Retorna el IdRequerimiento creado. Falla el assert si la respuesta no es 200.
    /// </summary>
    public async Task<int> CrearAsync(HttpClient client)
    {
        var dto = Build();
        var response = await client.PostAsJsonAsync("/api/compras/requerimientos", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear requerimiento (numero={dto.NumeroRequerimiento}). " +
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("idRequerimiento").GetInt32();
    }

    /// <summary>
    /// Crea el requerimiento y luego hace PATCH al estado 'EnviadoOC'.
    /// Útil cuando el siguiente paso es crear una OrdenCompra
    /// (el SP usp_OrdenCompra_Insertar exige estado='EnviadoOC' en el requerimiento).
    /// </summary>
    public async Task<int> CrearEnviadoOcAsync(HttpClient client)
    {
        var id = await CrearAsync(client);
        var estadoDto = new RequerimientoEstadoDto
        {
            Estado = "EnviadoOC",
            Observacion = "Cambio de estado automático (test setup)"
        };
        var response = await client.PatchAsync(
            $"/api/compras/requerimientos/{id}/estado",
            JsonContent.Create(estadoDto));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al cambiar estado a EnviadoOC. Body: {await response.Content.ReadAsStringAsync()}");
        return id;
    }
}
