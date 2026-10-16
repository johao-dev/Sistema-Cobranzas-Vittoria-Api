using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Actualizar;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="PermisoValidator"/>.
///
/// Validan que los comandos de actualizar cumplan las reglas de
/// validacion de application. Estos tests son puramente de logica; no
/// tocan repositorio ni base de datos.
/// </summary>
public class PermisoValidatorTests
{
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
