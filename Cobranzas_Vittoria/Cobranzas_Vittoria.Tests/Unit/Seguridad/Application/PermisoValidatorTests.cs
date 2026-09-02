using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Actualizar;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="PermisoValidator"/>.
///
/// Validan que los comandos de crear/actualizar cumplan las reglas de
/// validacion de application. Estos tests son puramente de logica; no
/// tocan repositorio ni base de datos.
/// </summary>
public class PermisoValidatorTests
{
    // =====================================================================
    // ValidarCreate
    // =====================================================================

    [Test]
    public void ValidarCreate_ComandoNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PermisoValidator.ValidarCreate(null!));
    }

    [Test]
    public void ValidarCreate_DatosValidos_NoLanzaExcepcion()
    {
        var command = new CreatePermisoCommand("permiso.test", "Nombre", "Desc");

        Assert.DoesNotThrow(() => PermisoValidator.ValidarCreate(command));
    }

    [Test]
    public void ValidarCreate_CodigoVacio_LanzaValidacionNegocioSeguridadException()
    {
        var command = new CreatePermisoCommand("", "Nombre", "Desc");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            PermisoValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.Codigo)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_CODIGO_REQUERIDO"));
    }

    [Test]
    public void ValidarCreate_CodigoConEspacios_LanzaValidacionNegocioSeguridadException()
    {
        var command = new CreatePermisoCommand("codigo espacio", "Nombre", "Desc");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            PermisoValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_CODIGO_ESPACIOS"));
    }

    [Test]
    public void ValidarCreate_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        var command = new CreatePermisoCommand("permiso.test", "", "Desc");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            PermisoValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.Nombre)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_NOMBRE_REQUERIDO"));
    }

    [Test]
    public void ValidarCreate_CodigoYNombreVacios_AgregaDosErrores()
    {
        var command = new CreatePermisoCommand("", "", "Desc");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            PermisoValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(2));
    }

    // =====================================================================
    // ValidarUpdate
    // =====================================================================

    [Test]
    public void ValidarUpdate_IdValido_NoLanzaExcepcion()
    {
        var command = new UpdatePermisoCommand(1, "Nuevo", "Desc");

        Assert.DoesNotThrow(() => PermisoValidator.ValidarUpdate(command));
    }

    [Test]
    public void ValidarUpdate_IdCero_LanzaValidacionNegocioSeguridadException()
    {
        var command = new UpdatePermisoCommand(0, "Nuevo", "Desc");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            PermisoValidator.ValidarUpdate(command))!;

        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.IdPermiso)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_ID_INVALIDO"));
    }

    [Test]
    public void ValidarUpdate_IdNegativo_LanzaValidacionNegocioSeguridadException()
    {
        var command = new UpdatePermisoCommand(-1, "Nuevo", "Desc");

        Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            PermisoValidator.ValidarUpdate(command));
    }
}
