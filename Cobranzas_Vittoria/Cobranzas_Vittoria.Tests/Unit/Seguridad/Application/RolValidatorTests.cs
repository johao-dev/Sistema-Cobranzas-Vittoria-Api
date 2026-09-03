using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Application.Rol.Actualizar;
using Cobranzas_Vittoria.Seguridad.Application.Rol.Crear;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="RolValidator"/>.
/// </summary>
public class RolValidatorTests
{
    [Test]
    public void ValidarCreate_ComandoNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RolValidator.ValidarCreate(null!));
    }

    [Test]
    public void ValidarCreate_DatosValidos_NoLanzaExcepcion()
    {
        var command = new CreateRolCommand("Nombre", "Desc");

        Assert.DoesNotThrow(() => RolValidator.ValidarCreate(command));
    }

    [Test]
    public void ValidarCreate_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        var command = new CreateRolCommand("", "Desc");

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            RolValidator.ValidarCreate(command))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.Nombre)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("ROL_NOMBRE_REQUERIDO"));
    }

    [Test]
    public void ValidarUpdate_IdValido_NoLanzaExcepcion()
    {
        var command = new ActualizarRolCommand(1, "Nombre", "Desc", true);

        Assert.DoesNotThrow(() => RolValidator.ValidarUpdate(command));
    }

    [Test]
    public void ValidarUpdate_IdCero_LanzaValidacionNegocioSeguridadException()
    {
        var command = new ActualizarRolCommand(0, "Nombre", "Desc", true);

        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            RolValidator.ValidarUpdate(command))!;

        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.IdRol)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("ROL_ID_INVALIDO"));
    }

    [Test]
    public void ValidarUpdate_IdNegativo_LanzaValidacionNegocioSeguridadException()
    {
        var command = new ActualizarRolCommand(-1, "Nombre", "Desc", true);

        Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            RolValidator.ValidarUpdate(command));
    }
}
