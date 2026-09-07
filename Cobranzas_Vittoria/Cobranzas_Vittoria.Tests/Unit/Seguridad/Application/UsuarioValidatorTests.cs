using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Application.Usuario.Actualizar;
using Cobranzas_Vittoria.Seguridad.Application.Usuario.Crear;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="UsuarioValidator"/>.
///
/// No requieren mocks: validan reglas puras de entrada.
/// </summary>
public class UsuarioValidatorTests
{
    // =====================================================================
    // ValidarCreate
    // =====================================================================

    [Test]
    public void ValidarCreate_DatosValidos_NoLanzaExcepcion()
    {
        var command = new CreateUsuarioCommand(
            "Juan",
            "Perez",
            "juan.perez@local",
            "jperez",
            "password");

        Assert.DoesNotThrow(() => UsuarioValidator.ValidarCreate(command));
    }

    [Test]
    public void ValidarCreate_NombreVacio_AgregaError()
    {
        var command = new CreateUsuarioCommand(
            "",
            "Perez",
            "juan.perez@local",
            "jperez",
            "password");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            UsuarioValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.Nombres)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_NOMBRE_REQUERIDO"));
    }

    [Test]
    public void ValidarCreate_ApellidoVacio_AgregaError()
    {
        var command = new CreateUsuarioCommand(
            "Juan",
            "",
            "juan.perez@local",
            "jperez",
            "password");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            UsuarioValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_APELLIDO_REQUERIDO"));
    }

    [Test]
    public void ValidarCreate_CorreoVacio_AgregaError()
    {
        var command = new CreateUsuarioCommand(
            "Juan",
            "Perez",
            "",
            "jperez",
            "password");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            UsuarioValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_CORREO_REQUERIDO"));
    }

    [Test]
    public void ValidarCreate_UsuarioLoginVacio_AgregaError()
    {
        var command = new CreateUsuarioCommand(
            "Juan",
            "Perez",
            "juan.perez@local",
            "",
            "password");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            UsuarioValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_LOGIN_REQUERIDO"));
    }

    [Test]
    public void ValidarCreate_PasswordHashVacio_AgregaError()
    {
        var command = new CreateUsuarioCommand(
            "Juan",
            "Perez",
            "juan.perez@local",
            "jperez",
            "");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            UsuarioValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_PASSWORD_REQUERIDO"));
    }

    [Test]
    public void ValidarCreate_TodosLosCamposVacios_AgregaCincoErrores()
    {
        var command = new CreateUsuarioCommand("", "", "", "", "");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            UsuarioValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(5));
    }

    // =====================================================================
    // ValidarUpdate
    // =====================================================================

    [Test]
    public void ValidarUpdate_IdValido_NoLanzaExcepcion()
    {
        var command = new ActualizarUsuarioCommand(
            1,
            "Juan",
            "Perez",
            null,
            null,
            null,
            null);

        Assert.DoesNotThrow(() => UsuarioValidator.ValidarUpdate(command));
    }

    [Test]
    public void ValidarUpdate_IdInvalido_AgregaError()
    {
        var command = new ActualizarUsuarioCommand(
            0,
            "Juan",
            "Perez",
            null,
            null,
            null,
            null);

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            UsuarioValidator.ValidarUpdate(command))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.IdUsuario)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_ID_INVALIDO"));
    }
}
