using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Domain;

/// <summary>
/// Pruebas unitarias del modelo de dominio <see cref="Permiso"/>.
///
/// Estas pruebas cubren las reglas de negocio encapsuladas en la entidad:
///   - Creacion valida/invalida (codigo/nombre requeridos, codigo sin espacios).
///   - Actualizacion parcial de nombre/descripcion.
///   - Activacion/desactivacion.
///   - Reconstruccion desde persistencia sin validar.
///
/// Son estables ante cambios de infraestructura (SPs, ORM, controllers).
/// </summary>
public class PermisoTests
{
    // =====================================================================
    // Permiso.Crear
    // =====================================================================

    [Test]
    public void Crear_DatosValidos_RetornaPermisoActivoConDatosTrim()
    {
        // Act
        var permiso = Permiso.Crear("permiso.test", "  Nombre  ", "  Desc  ");

        // Assert
        Assert.That(permiso.Codigo, Is.EqualTo("permiso.test"));
        Assert.That(permiso.Nombre, Is.EqualTo("Nombre"));
        Assert.That(permiso.Descripcion, Is.EqualTo("Desc"));
        Assert.That(permiso.Activo, Is.True);
        Assert.That(permiso.IdPermiso, Is.EqualTo(0));
    }

    [Test]
    public void Crear_DescripcionNula_UsaCadenaVacia()
    {
        var permiso = Permiso.Crear("permiso.test", "Nombre", null!);

        Assert.That(permiso.Descripcion, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Crear_CodigoVacio_LanzaValidacionNegocioSeguridadException()
    {
        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            Permiso.Crear("", "Nombre", "Desc"))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(Permiso.Codigo)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_CODIGO_REQUERIDO"));
    }

    [Test]
    public void Crear_CodigoConEspacios_LanzaValidacionNegocioSeguridadException()
    {
        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            Permiso.Crear("codigo con espacios", "Nombre", "Desc"))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_CODIGO_ESPACIOS"));
    }

    [Test]
    public void Crear_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            Permiso.Crear("permiso.test", "   ", "Desc"))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(Permiso.Nombre)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_NOMBRE_REQUERIDO"));
    }

    // =====================================================================
    // Permiso.ActualizarDatos
    // =====================================================================

    [Test]
    public void ActualizarDatos_NombreYDescripcion_RetornaValoresActualizados()
    {
        // Arrange
        var permiso = PermisoBuilder.Nuevo().BuildNewDomain();

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
        var permiso = PermisoBuilder.Nuevo()
            .ConDescripcion("Original")
            .BuildNewDomain();

        // Act
        permiso.ActualizarDatos("Nuevo nombre", "");

        // Assert
        Assert.That(permiso.Nombre, Is.EqualTo("Nuevo nombre"));
        Assert.That(permiso.Descripcion, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ActualizarDatos_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        var permiso = PermisoBuilder.Nuevo().BuildNewDomain();

        Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            permiso.ActualizarDatos("", "Desc"));
    }

    // =====================================================================
    // Activar / Desactivar
    // =====================================================================

    [Test]
    public void Desactivar_CambiaActivoAFalsoYRegistraModificacion()
    {
        var permiso = PermisoBuilder.Nuevo().BuildNewDomain();

        permiso.Desactivar();

        Assert.That(permiso.Activo, Is.False);
        Assert.That(permiso.FechaModificacion, Is.Not.Null);
    }

    [Test]
    public void Activar_CambiaActivoAVerdaderoYRegistraModificacion()
    {
        var permiso = PermisoBuilder.Nuevo().Inactivo().BuildNewDomain();

        permiso.Activar();

        Assert.That(permiso.Activo, Is.True);
        Assert.That(permiso.FechaModificacion, Is.Not.Null);
    }

    // =====================================================================
    // Auditoria
    // =====================================================================

    [Test]
    public void EstablecerAuditoriaCreacion_AsignaFechaYUsuario()
    {
        var permiso = PermisoBuilder.Nuevo().BuildNewDomain();
        var antes = DateTime.UtcNow.AddSeconds(-1);

        permiso.EstablecerAuditoriaCreacion("admin");

        Assert.That(permiso.UsuarioCreacion, Is.EqualTo("admin"));
        Assert.That(permiso.FechaCreacion, Is.GreaterThan(antes));
    }

    [Test]
    public void EstablecerAuditoriaModificacion_AsignaFechaYUsuario()
    {
        var permiso = PermisoBuilder.Nuevo().BuildNewDomain();
        var antes = DateTime.UtcNow.AddSeconds(-1);

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
        var permiso = Permiso.Reconstruir(
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
