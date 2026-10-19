using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Domain;

/// <summary>
/// Pruebas unitarias del modelo de dominio <see cref="Permiso"/>.
///
/// Estas pruebas cubren las reglas de negocio encapsuladas en la entidad:
///   - Actualizacion parcial de nombre/descripcion.
///   - Activacion/desactivacion.
///   - Auditoria de modificacion.
///   - Reconstruccion desde persistencia sin validar.
///
/// Son estables ante cambios de infraestructura (SPs, ORM, controllers).
/// </summary>
public class PermisoTests
{
    // =====================================================================
    // Permiso.ActualizarDatos
    // =====================================================================

    [Test]
    public void ActualizarDatos_NombreYDescripcion_RetornaValoresActualizados()
    {
        // Arrange
        Permiso permiso = PermisoBuilder.Nuevo().BuildDomain();

        // Act
        permiso.ActualizarDatos("Nuevo nombre", "Nueva descripcion");

        // Assert
        Assert.That(permiso.Nombre, Is.EqualTo("Nuevo nombre"));
        Assert.That(permiso.Descripcion, Is.EqualTo("Nueva descripcion"));
        Assert.That(permiso.FechaModificacion, Is.Not.Null);
    }

    [Test]
    public void ActualizarDatos_SoloNombre_DescripcionSeConservaSiSePasaVacia()
    {
        // Arrange
        Permiso permiso = PermisoBuilder.Nuevo()
            .ConDescripcion("Original")
            .BuildDomain();

        // Act
        permiso.ActualizarDatos("Nuevo nombre", "");

        // Assert
        Assert.That(permiso.Nombre, Is.EqualTo("Nuevo nombre"));
        Assert.That(permiso.Descripcion, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ActualizarDatos_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        Permiso permiso = PermisoBuilder.Nuevo().BuildDomain();

        Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            permiso.ActualizarDatos("", "Desc"));
    }

    // =====================================================================
    // Activar / Desactivar
    // =====================================================================

    [Test]
    public void Desactivar_CambiaActivoAFalsoYRegistraModificacion()
    {
        Permiso permiso = PermisoBuilder.Nuevo().BuildDomain();

        DateTime antes = DateTime.UtcNow;

        permiso.Desactivar("admin-test");

        Assert.That(permiso.Activo, Is.False);
        Assert.That(permiso.FechaModificacion, Is.GreaterThanOrEqualTo(antes));
        Assert.That(permiso.UsuarioModificacion, Is.EqualTo("admin-test"));
    }

    [Test]
    public void Activar_CambiaActivoAVerdaderoYRegistraModificacion()
    {
        Permiso permiso = PermisoBuilder.Nuevo().Inactivo().BuildDomain();

        Assert.That(permiso.Activo, Is.False);

        DateTime antes = DateTime.UtcNow;

        permiso.Activar("admin-test");

        Assert.That(permiso.Activo, Is.True);
        Assert.That(permiso.FechaModificacion, Is.GreaterThanOrEqualTo(antes));
        Assert.That(permiso.UsuarioModificacion, Is.EqualTo("admin-test"));
    }

    [TestCase(true, "PERMISO_YA_HABILITADO")]
    [TestCase(false, "PERMISO_YA_DESHABILITADO")]
    public void CambiarEstado_EstadoRepetido_RechazaSinAlterarAuditoria(bool habilitar, string codigoError)
    {
        DateTime fechaAnterior = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Permiso permiso = Permiso.Reconstruir(1, "permiso.test", "Nombre", "Descripcion", habilitar,
            fechaModificacion: fechaAnterior, usuarioModificacion: "usuario-anterior");

        ValidacionNegocioSeguridadException? excepcion = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
        {
            if (habilitar)
                permiso.Activar("otro-usuario");
            else
                permiso.Desactivar("otro-usuario");
        });

        Assert.Multiple(() =>
        {
            Assert.That(excepcion!.Errores.Single().CodigoError, Is.EqualTo(codigoError));
            Assert.That(excepcion.Errores.Single().Campo, Is.EqualTo(nameof(Permiso.Activo)));
            Assert.That(permiso.Activo, Is.EqualTo(habilitar));
            Assert.That(permiso.FechaModificacion, Is.EqualTo(fechaAnterior));
            Assert.That(permiso.UsuarioModificacion, Is.EqualTo("usuario-anterior"));
        });
    }

    // =====================================================================
    // Auditoria
    // =====================================================================

    [Test]
    public void EstablecerAuditoriaModificacion_AsignaFechaYUsuario()
    {
        Permiso permiso = PermisoBuilder.Nuevo().BuildDomain();
        DateTime antes = DateTime.UtcNow.AddSeconds(-1);

        permiso.EstablecerAuditoriaModificacion("admin");

        Assert.That(permiso.UsuarioModificacion, Is.EqualTo("admin"));
        Assert.That(permiso.FechaModificacion, Is.GreaterThan(antes));
    }

    // =====================================================================
    // Reconstruir
    // =====================================================================

    [Test]
    public void Reconstruir_DesdePersistencia_NoValidaCodigoNiNombre()
    {
        // Permite leer datos legacy sin aplicar las reglas actuales.
        Permiso permiso = Permiso.Reconstruir(
            idPermiso: 99,
            codigo: "legacy code",
            nombre: "",
            descripcion: "",
            activo: false);

        Assert.That(permiso.IdPermiso, Is.EqualTo(99));
        Assert.That(permiso.Codigo, Is.EqualTo("legacy code"));
        Assert.That(permiso.Nombre, Is.EqualTo(""));
    }
}
