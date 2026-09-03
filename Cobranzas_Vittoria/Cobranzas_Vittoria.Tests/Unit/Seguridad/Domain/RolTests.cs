using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Domain;

/// <summary>
/// Pruebas unitarias del modelo de dominio <see cref="Rol"/>.
///
/// Estas pruebas cubren las reglas de negocio encapsuladas en la entidad:
///   - Creacion valida/invalida (nombre requerido, trim).
///   - Actualizacion parcial de nombre/descripcion.
///   - Activacion/desactivacion.
///   - Gestion de permisos (agregar, quitar, evitar duplicados).
///   - Reconstruccion desde persistencia sin validar.
/// </summary>
public class RolTests
{
    // =====================================================================
    // Rol.Crear
    // =====================================================================

    [Test]
    public void Crear_DatosValidos_RetornaRolActivoConDatosTrim()
    {
        var rol = Rol.Crear("  Nombre  ", "  Desc  ");

        Assert.That(rol.Nombre, Is.EqualTo("Nombre"));
        Assert.That(rol.Descripcion, Is.EqualTo("Desc"));
        Assert.That(rol.Activo, Is.True);
        Assert.That(rol.IdRol, Is.EqualTo(0));
    }

    [Test]
    public void Crear_DescripcionNula_UsaCadenaVacia()
    {
        var rol = Rol.Crear("Nombre", null!);

        Assert.That(rol.Descripcion, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Crear_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            Rol.Crear("", "Desc"))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(Rol.Nombre)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("ROL_NOMBRE_REQUERIDO"));
    }

    [Test]
    public void Crear_ConPermisos_RetornaRolConPermisosAsociados()
    {
        var permisos = new[]
        {
            PermisoBuilder.Nuevo().ConCodigo("permiso.uno").BuildNewDomain(),
            PermisoBuilder.Nuevo().ConCodigo("permiso.dos").BuildNewDomain()
        };

        var rol = Rol.Crear("Rol con permisos", "Desc", permisos);

        Assert.That(rol.Permisos.Count(), Is.EqualTo(2));
    }

    // =====================================================================
    // Rol.ActualizarDatos
    // =====================================================================

    [Test]
    public void ActualizarDatos_NombreYDescripcion_RetornaValoresActualizados()
    {
        var rol = RolBuilder.Nuevo().BuildNewDomain();

        rol.ActualizarDatos("Nuevo nombre", "Nueva descripcion");

        Assert.That(rol.Nombre, Is.EqualTo("Nuevo nombre"));
        Assert.That(rol.Descripcion, Is.EqualTo("Nueva descripcion"));
        Assert.That(rol.FechaModificacion, Is.Not.Null);
    }

    [Test]
    public void ActualizarDatos_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        var rol = RolBuilder.Nuevo().BuildNewDomain();

        Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            rol.ActualizarDatos("", "Desc"));
    }

    // =====================================================================
    // Activar / Desactivar
    // =====================================================================

    [Test]
    public void Desactivar_CambiaActivoAFalsoYRegistraModificacion()
    {
        var rol = RolBuilder.Nuevo().BuildNewDomain();

        rol.Desactivar();

        Assert.That(rol.Activo, Is.False);
        Assert.That(rol.FechaModificacion, Is.Not.Null);
    }

    [Test]
    public void Activar_CambiaActivoAVerdaderoYRegistraModificacion()
    {
        var rol = RolBuilder.Nuevo().Inactivo().BuildNewDomain();

        rol.Activar();

        Assert.That(rol.Activo, Is.True);
        Assert.That(rol.FechaModificacion, Is.Not.Null);
    }

    // =====================================================================
    // Auditoria
    // =====================================================================

    [Test]
    public void EstablecerAuditoriaCreacion_AsignaFechaYUsuario()
    {
        var rol = RolBuilder.Nuevo().BuildNewDomain();
        var antes = DateTime.UtcNow.AddSeconds(-1);

        rol.EstablecerAuditoriaCreacion("admin");

        Assert.That(rol.UsuarioCreacion, Is.EqualTo("admin"));
        Assert.That(rol.FechaCreacion, Is.GreaterThan(antes));
    }

    [Test]
    public void EstablecerAuditoriaModificacion_AsignaFechaYUsuario()
    {
        var rol = RolBuilder.Nuevo().BuildNewDomain();
        var antes = DateTime.UtcNow.AddSeconds(-1);

        rol.EstablecerAuditoriaModificacion("admin");

        Assert.That(rol.UsuarioModificacion, Is.EqualTo("admin"));
        Assert.That(rol.FechaModificacion, Is.GreaterThan(antes));
    }

    // =====================================================================
    // Gestion de permisos
    // =====================================================================

    [Test]
    public void AgregarPermiso_NuevoPermiso_RetornaTrueYAumentaColeccion()
    {
        var rol = RolBuilder.Nuevo().BuildNewDomain();
        var permiso = PermisoBuilder.Nuevo().ConCodigo("permiso.nuevo").BuildNewDomain();

        var agregado = rol.AgregarPermiso(permiso);

        Assert.That(agregado, Is.True);
        Assert.That(rol.Permisos.Count(), Is.EqualTo(1));
    }

    [Test]
    public void AgregarPermiso_Duplicado_RetornaFalseYNoDuplica()
    {
        var permiso = PermisoBuilder.Nuevo().ConCodigo("permiso.dup").BuildNewDomain();
        var rol = RolBuilder.Nuevo()
            .ConPermiso(permiso)
            .BuildDomain();

        var agregado = rol.AgregarPermiso(permiso);

        Assert.That(agregado, Is.False);
        Assert.That(rol.Permisos.Count(), Is.EqualTo(1));
    }

    [Test]
    public void AgregarPermiso_Nulo_LanzaArgumentNullException()
    {
        var rol = RolBuilder.Nuevo().BuildNewDomain();

        Assert.Throws<ArgumentNullException>(() => rol.AgregarPermiso(null!));
    }

    [Test]
    public void QuitarPermiso_Existente_RetornaTrueYReduceColeccion()
    {
        var permiso = PermisoBuilder.Nuevo().ConCodigo("permiso.quitar").BuildNewDomain();
        var rol = RolBuilder.Nuevo()
            .ConPermiso(permiso)
            .BuildDomain();

        var quitado = rol.QuitarPermiso(permiso);

        Assert.That(quitado, Is.True);
        Assert.That(rol.Permisos, Is.Empty);
    }

    [Test]
    public void QuitarPermiso_Inexistente_RetornaFalse()
    {
        var rol = RolBuilder.Nuevo().BuildNewDomain();
        var permiso = PermisoBuilder.Nuevo().ConCodigo("permiso.inexistente").BuildNewDomain();

        var quitado = rol.QuitarPermiso(permiso);

        Assert.That(quitado, Is.False);
    }

    [Test]
    public void EstablecerPermisos_ReemplazaColeccionExistente()
    {
        var permisoOriginal = PermisoBuilder.Nuevo().ConCodigo("permiso.original").BuildNewDomain();
        var rol = RolBuilder.Nuevo()
            .ConPermiso(permisoOriginal)
            .BuildDomain();

        var permisosNuevos = new[]
        {
            PermisoBuilder.Nuevo().ConCodigo("permiso.nuevo1").BuildNewDomain(),
            PermisoBuilder.Nuevo().ConCodigo("permiso.nuevo2").BuildNewDomain()
        };

        rol.EstablecerPermisos(permisosNuevos);

        Assert.That(rol.Permisos.Count(), Is.EqualTo(2));
        Assert.That(rol.Permisos.Any(p => p.Codigo == "permiso.original"), Is.False);
    }

    [Test]
    public void LimpiarPermisos_VaciaLaColeccion()
    {
        var permiso = PermisoBuilder.Nuevo().ConCodigo("permiso.limpiar").BuildNewDomain();
        var rol = RolBuilder.Nuevo()
            .ConPermiso(permiso)
            .BuildDomain();

        rol.LimpiarPermisos();

        Assert.That(rol.Permisos, Is.Empty);
    }

    // =====================================================================
    // Reconstruir
    // =====================================================================

    [Test]
    public void Reconstruir_DesdePersistencia_NoValidaNombre()
    {
        var rol = Rol.Reconstruir(
            idRol: 99,
            nombre: "",
            descripcion: "",
            activo: false);

        Assert.That(rol.IdRol, Is.EqualTo(99));
        Assert.That(rol.Nombre, Is.EqualTo(""));
    }
}
