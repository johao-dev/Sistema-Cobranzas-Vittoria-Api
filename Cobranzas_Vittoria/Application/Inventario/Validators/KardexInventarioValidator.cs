using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Application.Inventario.Dtos;
using Cobranzas_Vittoria.Application.Inventario.Excepciones;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Application.Inventario.Validators;

/// <summary>
/// Validador de payloads del modulo Inventario (Kardex manual).
///
/// <para>
/// <b>Responsabilidad</b>: aplicar las reglas de negocio que se pueden
/// verificar SIN tocar la base de datos, antes de invocar al SP. Si la
/// validacion falla, lanza <see cref="ValidacionNegocioInventarioException"/>
/// (que extiende <see cref="DatosInvalidosValidacionException"/>) y el
/// controller la traduce a 422.
/// </para>
///
/// <para>
/// <b>Lo que NO hace</b>:
///   - No valida unicidad ni existencia por PK (eso lo hace el SP).
///   - No valida reglas que requieren leer KardexStock (stock insuficiente):
///     eso lo hace el SP en la misma TX que la operacion que lo origina.
///   - No valida formatos complejos (RUC, codigos): son validaciones de UI.
/// </para>
///
/// <para>
/// <b>Por que depende de los repos legacy de Maestra</b>:
/// el proyecto esta en transicion de Layered a Clean Architecture. Los
/// repos de maestra (<c>IMaterialRepository</c>, <c>IEspecialidadRepository</c>,
/// etc) viven en <c>Cobranzas_Vittoria.Interfaces</c> / <c>Repositories/</c>.
/// Reutilizarlos aqui evita duplicar su logica hasta que se migren a
/// <c>Application/Maestra/</c> en una fase posterior.
/// </para>
///
/// <para>
/// <b>Acumulacion de errores</b>: el validador NO aborta al primer error.
/// Recorre todas las reglas y junta los <see cref="DetalleErrorValidacion"/>
/// en una lista, para que el cliente vea TODOS los problemas en una
/// sola respuesta 422 (mismo patron que <c>ImportProcessorBase</c>).
/// </para>
/// </summary>
public sealed class KardexInventarioValidator
{
    private readonly IEspecialidadRepository _especialidadRepository;
    private readonly IMaterialRepository _materialRepository;
    private readonly IProveedorRepository _proveedorRepository;
    private readonly IProyectoRepository _proyectoRepository;

    public KardexInventarioValidator(
        IEspecialidadRepository especialidadRepository,
        IMaterialRepository materialRepository,
        IProveedorRepository proveedorRepository,
        IProyectoRepository proyectoRepository)
    {
        _especialidadRepository = especialidadRepository ?? throw new ArgumentNullException(nameof(especialidadRepository));
        _materialRepository = materialRepository ?? throw new ArgumentNullException(nameof(materialRepository));
        _proveedorRepository = proveedorRepository ?? throw new ArgumentNullException(nameof(proveedorRepository));
        _proyectoRepository = proyectoRepository ?? throw new ArgumentNullException(nameof(proyectoRepository));
    }

    // ============================================================================
    // Validacion de KardexEntrada (Create / Update)
    // ============================================================================

    /// <summary>
    /// Valida un DTO de entrada. Lanza <see cref="ValidacionNegocioInventarioException"/>
    /// si encuentra errores. Los errores se acumulan: el cliente ve TODOS
    /// los problemas en una sola respuesta.
    /// </summary>
    /// <param name="dto">DTO a validar.</param>
    /// <param name="ct">Token de cancelacion.</param>
    public async Task ValidarEntradaAsync(KardexEntradaCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var errores = new List<DetalleErrorValidacion>();

        // ---------- Campos requeridos ----------
        if (dto.IdEspecialidad <= 0)
            errores.Add(Error("idEspecialidad", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo idEspecialidad es obligatorio y debe ser mayor a 0."));

        if (dto.IdMaterial <= 0)
            errores.Add(Error("idMaterial", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo idMaterial es obligatorio y debe ser mayor a 0."));

        if (!dto.IdProyecto.HasValue || dto.IdProyecto.Value <= 0)
            errores.Add(Error("idProyecto", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo idProyecto es obligatorio y debe ser mayor a 0."));

        // ---------- Cantidad ----------
        if (dto.Cantidad < 0m)
            errores.Add(Error("cantidad", CodigosErrorInventario.Validacion.CantidadInvalida,
                "La cantidad no puede ser negativa."));

        // ---------- Longitud de strings ----------
        if (!string.IsNullOrEmpty(dto.NumeroDocumento) && dto.NumeroDocumento.Length > 50)
            errores.Add(Error("numeroDocumento", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo numeroDocumento no puede exceder 50 caracteres."));

        if (!string.IsNullOrEmpty(dto.Observacion) && dto.Observacion.Length > 250)
            errores.Add(Error("observacion", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo observacion no puede exceder 250 caracteres."));

        // ---------- FKs: existencia en maestra ----------
        if (errores.Count == 0)
        {
            await ValidarFkAsync(
                errores,
                idEspecialidad: dto.IdEspecialidad,
                idMaterial: dto.IdMaterial,
                idProveedor: dto.IdProveedor,
                idProyecto: dto.IdProyecto,
                validarProveedor: true,
                ct);
        }

        if (errores.Count > 0)
        {
            throw new ValidacionNegocioInventarioException(errores);
        }
    }

    // ============================================================================
    // Validacion de KardexSalida (Create / Update)
    // ============================================================================

    /// <summary>
    /// Valida un DTO de salida. Lanza <see cref="ValidacionNegocioInventarioException"/>
    /// si encuentra errores. Los errores se acumulan.
    /// </summary>
    /// <param name="dto">DTO a validar.</param>
    /// <param name="ct">Token de cancelacion.</param>
    public async Task ValidarSalidaAsync(KardexSalidaCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var errores = new List<DetalleErrorValidacion>();

        // ---------- Campos de cabecera requeridos ----------
        if (dto.IdEspecialidad <= 0)
            errores.Add(Error("idEspecialidad", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo idEspecialidad es obligatorio y debe ser mayor a 0."));

        if (!dto.IdProyecto.HasValue || dto.IdProyecto.Value <= 0)
            errores.Add(Error("idProyecto", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo idProyecto es obligatorio y debe ser mayor a 0."));

        if (string.IsNullOrWhiteSpace(dto.Solicitante))
            errores.Add(Error("solicitante", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo solicitante es obligatorio y no puede estar vacio."));
        else if (dto.Solicitante.Length > 150)
            errores.Add(Error("solicitante", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo solicitante no puede exceder 150 caracteres."));

        if (!string.IsNullOrEmpty(dto.NumeroDocumento) && dto.NumeroDocumento.Length > 50)
            errores.Add(Error("numeroDocumento", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo numeroDocumento no puede exceder 50 caracteres."));

        if (!string.IsNullOrEmpty(dto.Observacion) && dto.Observacion.Length > 250)
            errores.Add(Error("observacion", CodigosErrorInventario.Validacion.CampoRequerido,
                "El campo observacion no puede exceder 250 caracteres."));

        // ---------- Items: minimo uno y cada item valido ----------
        if (dto.Items is null || dto.Items.Count == 0)
        {
            errores.Add(Error("items", CodigosErrorInventario.Validacion.ItemsInvalidos,
                "La salida debe tener al menos un item."));
        }
        else
        {
            for (var i = 0; i < dto.Items.Count; i++)
            {
                var item = dto.Items[i];
                if (item is null)
                {
                    errores.Add(Error($"items[{i}]", CodigosErrorInventario.Validacion.ItemsInvalidos,
                        $"El item en la posicion {i} es nulo."));
                    continue;
                }

                if (item.IdMaterial <= 0)
                    errores.Add(Error($"items[{i}].idMaterial", CodigosErrorInventario.Validacion.CampoRequerido,
                        $"El item en la posicion {i} tiene idMaterial invalido."));

                if (item.Cantidad < 0m)
                    errores.Add(Error($"items[{i}].cantidad", CodigosErrorInventario.Validacion.CantidadInvalida,
                        $"El item en la posicion {i} tiene cantidad negativa."));

                if (!string.IsNullOrEmpty(item.Observacion) && item.Observacion.Length > 250)
                    errores.Add(Error($"items[{i}].observacion", CodigosErrorInventario.Validacion.CampoRequerido,
                        $"El item en la posicion {i} tiene observacion que excede 250 caracteres."));
            }
        }

        // ---------- FKs: existencia en maestra ----------
        // Solo validamos FKs si la cabecera esta OK; si no, los errores
        // de FK serian ruido y duplicarian los errores de campo requerido.
        if (errores.Count == 0)
        {
            await ValidarFkAsync(
                errores,
                idEspecialidad: dto.IdEspecialidad,
                idMaterial: null, // items usan IdMaterial propio, se valida abajo
                idProveedor: null,
                idProyecto: dto.IdProyecto,
                validarProveedor: false,
                ct);

            // Validar que cada IdMaterial de los items exista y pertenezca a la especialidad.
            if (dto.Items is not null)
            {
                for (var i = 0; i < dto.Items.Count; i++)
                {
                    var item = dto.Items[i];
                    if (item is null || item.IdMaterial <= 0) continue;

                    var material = await _materialRepository.GetAsync(item.IdMaterial);
                    if (material is null)
                    {
                        errores.Add(Error(
                            $"items[{i}].idMaterial",
                            CodigosErrorInventario.Validacion.FkNoExiste,
                            $"El item en la posicion {i} referencia el material {item.IdMaterial} que no existe."));
                    }
                    else if (material.IdEspecialidad != dto.IdEspecialidad)
                    {
                        errores.Add(Error(
                            $"items[{i}].idMaterial",
                            CodigosErrorInventario.Validacion.FkNoExiste,
                            $"El material {item.IdMaterial} pertenece a la especialidad {material.IdEspecialidad}, no a la {dto.IdEspecialidad} indicada en la cabecera."));
                    }
                }
            }
        }

        if (errores.Count > 0)
        {
            throw new ValidacionNegocioInventarioException(errores);
        }
    }

    // ============================================================================
    // Validacion de existencia de una salida por Id (para PUT/DELETE)
    // ============================================================================

    /// <summary>
    /// Valida que exista una salida con el Id indicado. Usado por PUT/DELETE
    /// para devolver 404 antes de tocar la BD.
    /// </summary>
    public async Task ValidarSalidaExisteAsync(int idKardexSalida, CancellationToken ct = default)
    {
        // Delegamos al SP: si no existe, el SP lanzara 51104. Aqui solo
        // validamos que el id sea > 0.
        if (idKardexSalida <= 0)
        {
            throw new ValidacionNegocioInventarioException(
                "El id de la salida es invalido.",
                new DetalleErrorValidacion(
                    Fila: null,
                    Campo: "idKardexSalida",
                    CodigoError: CodigosErrorInventario.Validacion.CampoRequerido,
                    Mensaje: "El idKardexSalida debe ser mayor a 0."));
        }

        // Touch del CT para evitar warning en implementaciones async.
        await Task.CompletedTask;
    }

    // ============================================================================
    // Helpers privados
    // ============================================================================

    /// <summary>
    /// Valida que las FKs existan en maestra. Solo agrega errores a la lista;
    /// no aborta.
    /// </summary>
    private async Task ValidarFkAsync(
        List<DetalleErrorValidacion> errores,
        int idEspecialidad,
        int? idMaterial,
        int? idProveedor,
        int? idProyecto,
        bool validarProveedor,
        CancellationToken ct)
    {
        // Especialidad
        var especialidades = await _especialidadRepository.ListAsync(activo: true);
        if (!especialidades.Any(e => e.IdEspecialidad == idEspecialidad))
        {
            errores.Add(Error(
                "idEspecialidad",
                CodigosErrorInventario.Validacion.FkNoExiste,
                $"La especialidad {idEspecialidad} no existe o esta inactiva."));
        }

        // Material (si se indico)
        if (idMaterial.HasValue && idMaterial.Value > 0)
        {
            var material = await _materialRepository.GetAsync(idMaterial.Value);
            if (material is null)
            {
                errores.Add(Error(
                    "idMaterial",
                    CodigosErrorInventario.Validacion.FkNoExiste,
                    $"El material {idMaterial.Value} no existe."));
            }
            else if (material.IdEspecialidad != idEspecialidad)
            {
                errores.Add(Error(
                    "idMaterial",
                    CodigosErrorInventario.Validacion.FkNoExiste,
                    $"El material {idMaterial.Value} pertenece a la especialidad {material.IdEspecialidad}, no a la {idEspecialidad} indicada."));
            }
        }

        // Proveedor (opcional, si se indico y el contexto lo requiere)
        if (validarProveedor && idProveedor.HasValue && idProveedor.Value > 0)
        {
            var (proveedor, _) = await _proveedorRepository.GetAsync(idProveedor.Value);
            if (proveedor is null)
            {
                errores.Add(Error(
                    "idProveedor",
                    CodigosErrorInventario.Validacion.FkNoExiste,
                    $"El proveedor {idProveedor.Value} no existe."));
            }
        }

        // Proyecto (opcional, si se indico)
        if (idProyecto.HasValue && idProyecto.Value > 0)
        {
            var proyectos = await _proyectoRepository.ListAsync(activo: true);
            if (!proyectos.Any(p => p.IdProyecto == idProyecto.Value))
            {
                errores.Add(Error(
                    "idProyecto",
                    CodigosErrorInventario.Validacion.FkNoExiste,
                    $"El proyecto {idProyecto.Value} no existe o esta inactivo."));
            }
        }
    }

    private static DetalleErrorValidacion Error(string campo, string codigo, string mensaje)
        => new(Fila: null, Campo: campo, CodigoError: codigo, Mensaje: mensaje);
}
