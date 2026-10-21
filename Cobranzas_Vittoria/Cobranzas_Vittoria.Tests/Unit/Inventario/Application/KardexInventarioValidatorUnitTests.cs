using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Application.Excepciones;
using Cobranzas_Vittoria.Inventario.Application.Common;
using Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;

namespace Cobranzas_Vittoria.Tests.Unit.Inventario.Application;

/// <summary>
/// Pruebas unitarias de <see cref="KardexInventarioValidator"/>.
///
/// El validador NO aborta al primer error: acumula todos los
/// <see cref="DetalleErrorValidacion"/> y los lanza juntos en una sola
/// <see cref="ValidacionNegocioInventarioException"/>. Esto es importante
/// para que el cliente vea TODOS los problemas en una sola respuesta 422.
///
/// Convenciones:
///   - Cada test arma los stubs de repos legacy de Maestra con los datos
///     minimos que necesita el escenario.
///   - Se valida codigo, campo, mensaje y (cuando aplica) cantidad de errores
///     para asegurar que la acumulacion funciona correctamente.
///   - La clase ValidarFkAsync solo se ejecuta si los campos requeridos
///     estan OK; si hay errores de campos, los FKs no se validan
///     (documentado en el codigo del validator).
/// </summary>
public class KardexInventarioValidatorUnitTests
{
    // IDs canonicos del escenario "todo OK"
    private const int IdEspecialidadOk = 2;
    private const int IdMaterialOk = 100;
    private const int IdProveedorOk = 5;
    private const int IdProyectoOk = 10;

    // =========================================================================
    // Helpers de construccion
    // =========================================================================

    /// <summary>
    /// Crea un validator con un set minimo de datos validos (1 especialidad,
    /// 1 material que pertenece a esa especialidad, 1 proveedor, 1 proyecto).
    /// Los tests modifican la coleccion para provocar errores de FK.
    /// </summary>
    private KardexInventarioValidator CrearValidatorConDatosValidos()
    {
        var esp = new StubEspecialidadRepository();
        esp.Add(IdEspecialidadOk, "Albañileria");

        var mat = new StubMaterialRepository();
        mat.Add(IdMaterialOk, IdEspecialidadOk, "Cemento");

        var prov = new StubProveedorRepository();
        prov.Add(IdProveedorOk, "Proveedor X");

        var proy = new StubProyectoRepository();
        proy.Add(IdProyectoOk, "Proyecto Test");

        return new KardexInventarioValidator(esp, mat, prov, proy);
    }

    private static RegistroKardexEntrada EntradaValida()
        => new()
        {
            IdMaterial = IdMaterialOk,
            IdProveedor = IdProveedorOk,
            IdProyecto = IdProyectoOk,
            NumeroDocumento = "DOC-001",
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            Cantidad = 10m,
            Observacion = "Obs valida"
        };

    private static RegistroKardexSalida SalidaValida()
        => new()
        {
            IdProyecto = IdProyectoOk,
            NumeroDocumento = "SAL-001",
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            Solicitante = "Juan Perez",
            Observacion = "Obs valida",
            Items = new List<ItemKardexSalida>
            {
                new() { IdMaterial = IdMaterialOk, Cantidad = 5m, Observacion = "item 1" }
            }
        };

    // =========================================================================
    // Happy path
    // =========================================================================

    [Test]
    public void ValidarEntrada_DtoValido_NoLanza()
    {
        var validator = CrearValidatorConDatosValidos();

        Assert.DoesNotThrowAsync(async () =>
            await validator.ValidarEntradaAsync(EntradaValida()));
    }

    [Test]
    public void ValidarSalida_DtoValido_NoLanza()
    {
        var validator = CrearValidatorConDatosValidos();

        Assert.DoesNotThrowAsync(async () =>
            await validator.ValidarSalidaAsync(SalidaValida()));
    }

    // =========================================================================
    // ValidarEntrada: campos requeridos
    // =========================================================================



    [Test]
    public void ValidarEntrada_IdMaterialCero_AcumulaError()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.IdMaterial = 0;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarEntradaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "idMaterial"), Is.True);
    }

    [Test]
    public void ValidarEntrada_CantidadNegativa_AcumulaError()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.Cantidad = -1m;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarEntradaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "cantidad"), Is.True);
        Assert.That(ex.Errores.First(e => e.Campo == "cantidad").CodigoError,
            Is.EqualTo(CodigosErrorInventario.Validacion.CantidadInvalida));
    }

    [Test]
    public void ValidarEntrada_NumeroDocumentoMayor50_AcumulaError()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.NumeroDocumento = new string('X', 51);

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarEntradaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "numeroDocumento"), Is.True);
    }

    [Test]
    public void ValidarEntrada_ObservacionMayor250_AcumulaError()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.Observacion = new string('A', 251);

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarEntradaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "observacion"), Is.True);
    }

    [Test]
    public void ValidarEntrada_MultiplesErrores_LosAcumulaTodos()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.IdMaterial = -5;
        dto.Cantidad = -1m;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarEntradaAsync(dto))!;

        Assert.That(ex.Errores.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(ex.Errores.Any(e => e.Campo == "idMaterial"), Is.True);
        Assert.That(ex.Errores.Any(e => e.Campo == "cantidad"), Is.True);
    }

    [Test]
    public void ValidarEntrada_DtoNull_LanzaArgumentNull()
    {
        var validator = CrearValidatorConDatosValidos();
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await validator.ValidarEntradaAsync(null!));
    }

    // =========================================================================
    // ValidarEntrada: FKs
    // =========================================================================

    [Test]
    public void ValidarEntrada_EspecialidadDelMaterialInactiva_AcumulaErrorEnMaterial()
    {
        StubEspecialidadRepository especialidades = new();
        StubMaterialRepository materiales = new();
        materiales.Add(IdMaterialOk, IdEspecialidadOk, "Material");
        StubProyectoRepository proyectos = new();
        proyectos.Add(IdProyectoOk, "Proyecto");
        KardexInventarioValidator validator = new(especialidades, materiales, new StubProveedorRepository(), proyectos);
        RegistroKardexEntrada entrada = EntradaValida();
        entrada.IdProveedor = null;

        ValidacionNegocioInventarioException? error = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(
            () => validator.ValidarEntradaAsync(entrada));

        Assert.That(error!.Errores.Any(e => e.Campo == "idMaterial" && e.CodigoError == "FK_NO_EXISTE"), Is.True);
    }

    [Test]
    public void ValidarEntrada_MaterialNoExiste_AcumulaErrorFk()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.IdMaterial = 9999; // No existe en el stub.

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarEntradaAsync(dto))!;

        Assert.That(ex.Errores.Any(e =>
            e.Campo == "idMaterial" &&
            e.CodigoError == CodigosErrorInventario.Validacion.FkNoExiste), Is.True);
    }

    [Test]
    public void ValidarEntrada_DerivaEspecialidadDelMaterial_NoLanza()
    {
        // La clasificación se obtiene del material, sin enviarla en el request.
        var esp = new StubEspecialidadRepository();
        esp.Add(IdEspecialidadOk, "Alba");
        esp.Add(99, "Otra");
        var mat = new StubMaterialRepository();
        mat.Add(200, 99, "Material Otro");
        var prov = new StubProveedorRepository();
        prov.Add(IdProveedorOk, "P");
        var proy = new StubProyectoRepository();
        proy.Add(IdProyectoOk, "Pry");
        var validator2 = new KardexInventarioValidator(esp, mat, prov, proy);

        var dto = EntradaValida();
        dto.IdMaterial = 200;

        Assert.DoesNotThrowAsync(() => validator2.ValidarEntradaAsync(dto));
    }

    [Test]
    public void ValidarEntrada_ProveedorNoExiste_AcumulaErrorFk()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.IdProveedor = 777;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarEntradaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "idProveedor"), Is.True);
    }

    [Test]
    public void ValidarEntrada_ProyectoNoExiste_AcumulaErrorFk()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.IdProyecto = 888;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarEntradaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "idProyecto"), Is.True);
    }

    [Test]
    public void ValidarEntrada_CamposInvalidos_NoEjecutaFks_DocumentaAcumulacion()
    {
        // Si ya hay errores de campos, el validator NO llama a ValidarFkAsync
        // (los errores de FK serian ruido y duplicarian los de campo).
        // Un material inválido produce un error de campo, sin consultar sus FK.
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.IdMaterial = 0;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarEntradaAsync(dto))!;

        // Solo el error de campo, sin errores de FK adicionales.
        Assert.That(ex.Errores.Count, Is.EqualTo(1));
        Assert.That(ex.Errores.Any(e => e.CodigoError == CodigosErrorInventario.Validacion.FkNoExiste),
            Is.False);
    }

    // =========================================================================
    // ValidarSalida: campos requeridos + items
    // =========================================================================

    [Test]
    public void ValidarSalida_SolicitanteVacio_AcumulaError()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = SalidaValida();
        dto.Solicitante = string.Empty;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarSalidaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "solicitante"), Is.True);
    }

    [Test]
    public void ValidarSalida_SolicitanteMayor150_AcumulaError()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = SalidaValida();
        dto.Solicitante = new string('S', 151);

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarSalidaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "solicitante"), Is.True);
    }

    [Test]
    public void ValidarSalida_SinItems_AcumulaErrorItemsInvalidos()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = SalidaValida();
        dto.Items = new List<ItemKardexSalida>();

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarSalidaAsync(dto))!;

        Assert.That(ex.Errores.Any(e =>
            e.Campo == "items" &&
            e.CodigoError == CodigosErrorInventario.Validacion.ItemsInvalidos), Is.True);
    }

    [Test]
    public void ValidarSalida_ItemsNulo_AcumulaErrorItemsInvalidos()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = SalidaValida();
        dto.Items = null;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarSalidaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.CodigoError == CodigosErrorInventario.Validacion.ItemsInvalidos),
            Is.True);
    }

    [Test]
    public void ValidarSalida_ItemConIdMaterialCero_AcumulaErrorPosicional()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = SalidaValida();
        dto.Items![0].IdMaterial = 0;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarSalidaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "items[0].idMaterial"), Is.True);
    }

    [Test]
    public void ValidarSalida_ItemConCantidadNegativa_AcumulaErrorPosicional()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = SalidaValida();
        dto.Items![0].Cantidad = -2m;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarSalidaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "items[0].cantidad"), Is.True);
    }

    [Test]
    public void ValidarSalida_ItemConObservacionLarga_AcumulaErrorPosicional()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = SalidaValida();
        dto.Items![0].Observacion = new string('O', 251);

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarSalidaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "items[0].observacion"), Is.True);
    }

    [Test]
    public void ValidarSalida_MaterialesDeVariasEspecialidades_NoLanza()
    {
        var esp = new StubEspecialidadRepository();
        esp.Add(IdEspecialidadOk, "Alba");
        esp.Add(99, "Otra");
        var mat = new StubMaterialRepository();
        mat.Add(IdMaterialOk, IdEspecialidadOk, "OK");
        mat.Add(200, 99, "Otro");
        var prov = new StubProveedorRepository();
        var proy = new StubProyectoRepository();
        proy.Add(IdProyectoOk, "Pry");
        var validator = new KardexInventarioValidator(esp, mat, prov, proy);

        var dto = SalidaValida();
        dto.Items = new List<ItemKardexSalida>
        {
            new() { IdMaterial = IdMaterialOk, Cantidad = 1m },
            new() { IdMaterial = 200, Cantidad = 1m }
        };

        Assert.DoesNotThrowAsync(() => validator.ValidarSalidaAsync(dto));
    }

    [Test]
    public void ValidarSalida_ProyectoInvalido_AcumulaErrorFk()
    {
        // Para Salida se valida idProyecto pero no idProveedor (no aparece en la cabecera).
        var validator = CrearValidatorConDatosValidos();
        var dto = SalidaValida();
        dto.IdProyecto = 5555;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarSalidaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "idProyecto"), Is.True);
        // Verifica que NO se pidio idProveedor (es null en la salida, no debe marcarse).
        Assert.That(ex.Errores.Any(e => e.Campo == "idProveedor"), Is.False);
    }

    // =========================================================================
    // ValidarSalidaExisteAsync
    // =========================================================================

    [Test]
    public void ValidarSalidaExiste_IdCero_Lanza422()
    {
        var validator = CrearValidatorConDatosValidos();

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarSalidaExisteAsync(0))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "idKardexSalida"), Is.True);
    }

    [Test]
    public void ValidarSalidaExiste_IdPositivo_NoLanza()
    {
        var validator = CrearValidatorConDatosValidos();
        Assert.DoesNotThrowAsync(async () =>
            await validator.ValidarSalidaExisteAsync(42));
    }

    [Test]
    public void ValidarEntrada_ProveedorOpcional_NoLanzaSiEsNull()
    {
        // idProveedor es opcional en KardexEntrada. Si es null, NO se valida FK.
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.IdProveedor = null;

        Assert.DoesNotThrowAsync(async () =>
            await validator.ValidarEntradaAsync(dto));
    }

    [Test]
    public void ValidarEntrada_ProyectoRequerido_LanzaSiEsNull()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = EntradaValida();
        dto.IdProyecto = null;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarEntradaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "idProyecto"), Is.True);
    }

    [Test]
    public void ValidarSalida_ProyectoRequerido_LanzaSiEsNull()
    {
        var validator = CrearValidatorConDatosValidos();
        var dto = SalidaValida();
        dto.IdProyecto = null;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await validator.ValidarSalidaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "idProyecto"), Is.True);
    }
}
