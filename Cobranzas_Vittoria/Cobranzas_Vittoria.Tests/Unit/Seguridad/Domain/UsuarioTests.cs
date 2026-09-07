using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Domain;

/// <summary>
/// Pruebas unitarias del modelo de dominio <see cref="Usuario"/>.
///
/// Estas pruebas cubren las reglas de negocio encapsuladas en la entidad:
///   - Creacion valida/invalida (nombre, apellido, correo, login, password).
///   - Actualizacion de datos personales y correo.
///   - Gestion de roles (asignar, quitar, verificar).
///   - Activacion/desactivacion.
///   - Reconstruccion desde persistencia sin validar.
///
/// Son estables ante cambios de infraestructura (SPs, ORM, controllers).
/// </summary>
public class UsuarioTests
{
    // =====================================================================
    // Usuario.Crear
    // =====================================================================

    [Test]
    public void Crear_DatosValidos_RetornaUsuarioActivoConDatosTrim()
    {
        // Act
        var usuario = Usuario.Crear(
            "  Juan  ",
            "  Perez  ",
            "juan.perez@local",
            "  jperez  ",
            "password-hash",
            "admin-test");

        // Assert
        Assert.That(usuario.Nombres, Is.EqualTo("Juan"));
        Assert.That(usuario.Apellidos, Is.EqualTo("Perez"));
        Assert.That(usuario.Correo.Value, Is.EqualTo("juan.perez@local"));
        Assert.That(usuario.UsuarioLogin, Is.EqualTo("jperez"));
        Assert.That(usuario.PasswordHash, Is.EqualTo("password-hash"));
        Assert.That(usuario.Activo, Is.True);
        Assert.That(usuario.IdUsuario, Is.EqualTo(0));
        Assert.That(usuario.UsuarioCreacion, Is.EqualTo("admin-test"));
        Assert.That(usuario.FechaCreacion, Is.Not.Null);
    }

    [Test]
    public void Crear_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            Usuario.Crear("", "Perez", "juan@local", "jperez", "password", "admin"))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(Usuario.Nombres)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_NOMBRE_REQUERIDO"));
    }

    [Test]
    public void Crear_ApellidoVacio_LanzaValidacionNegocioSeguridadException()
    {
        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            Usuario.Crear("Juan", "", "juan@local", "jperez", "password", "admin"))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(Usuario.Apellidos)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_APELLIDO_REQUERIDO"));
    }

    [Test]
    public void Crear_UsuarioLoginVacio_LanzaValidacionNegocioSeguridadException()
    {
        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            Usuario.Crear("Juan", "Perez", "juan@local", "", "password", "admin"))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(Usuario.UsuarioLogin)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_LOGIN_REQUERIDO"));
    }

    [Test]
    public void Crear_PasswordHashVacio_LanzaValidacionNegocioSeguridadException()
    {
        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            Usuario.Crear("Juan", "Perez", "juan@local", "jperez", "", "admin"))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(Usuario.PasswordHash)));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_PASSWORD_REQUERIDO"));
    }

    [Test]
    public void Crear_CorreoInvalido_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Usuario.Crear("Juan", "Perez", "no-es-un-correo", "jperez", "password", "admin"));
    }

    [Test]
    public void Crear_UsuarioCreacionVacio_LanzaValidacionNegocioSeguridadException()
    {
        var ex = Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            Usuario.Crear("Juan", "Perez", "juan@local", "jperez", "password", ""))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_AUDITORIA_CREACION_REQUERIDO"));
    }

    // =====================================================================
    // Usuario.ActualizarDatos
    // =====================================================================

    [Test]
    public void ActualizarDatos_NombreYApellidos_RetornaValoresActualizados()
    {
        // Arrange
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();

        // Act
        usuario.ActualizarDatos("Carlos", "Lopez");

        // Assert
        Assert.That(usuario.Nombres, Is.EqualTo("Carlos"));
        Assert.That(usuario.Apellidos, Is.EqualTo("Lopez"));
    }

    [Test]
    public void ActualizarDatos_AplicaTrimANombreYApellidos()
    {
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();

        usuario.ActualizarDatos("  Carlos  ", "  Lopez  ");

        Assert.That(usuario.Nombres, Is.EqualTo("Carlos"));
        Assert.That(usuario.Apellidos, Is.EqualTo("Lopez"));
    }

    [Test]
    public void ActualizarDatos_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();

        Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            usuario.ActualizarDatos("", "Lopez"));
    }

    // =====================================================================
    // Usuario.ActualizarCorreo
    // =====================================================================

    [Test]
    public void ActualizarCorreo_CorreoValido_ActualizaCorreo()
    {
        // Arrange
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();

        // Act
        usuario.ActualizarCorreo("nuevo.correo@local");

        // Assert
        Assert.That(usuario.Correo.Value, Is.EqualTo("nuevo.correo@local"));
    }

    [Test]
    public void ActualizarCorreo_CorreoInvalido_LanzaArgumentException()
    {
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();

        Assert.Throws<ArgumentException>(() =>
            usuario.ActualizarCorreo("no-es-un-correo"));
    }

    // =====================================================================
    // Usuario.AsignarCredenciales
    // =====================================================================

    [Test]
    public void AsignarCredenciales_DatosValidos_ActualizaLoginYPassword()
    {
        // Arrange
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();

        // Act
        usuario.AsignarCredenciales("nuevo.login", "nuevo-hash");

        // Assert
        Assert.That(usuario.UsuarioLogin, Is.EqualTo("nuevo.login"));
        Assert.That(usuario.PasswordHash, Is.EqualTo("nuevo-hash"));
    }

    [Test]
    public void AsignarCredenciales_UsuarioLoginVacio_LanzaValidacionNegocioSeguridadException()
    {
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();

        Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            usuario.AsignarCredenciales("", "hash"));
    }

    [Test]
    public void AsignarCredenciales_PasswordHashVacio_LanzaValidacionNegocioSeguridadException()
    {
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();

        Assert.Throws<ValidacionNegocioSeguridadException>(() =>
            usuario.AsignarCredenciales("login", ""));
    }

    // =====================================================================
    // Roles
    // =====================================================================

    [Test]
    public void AsignarRol_RolNuevo_AgregaRol()
    {
        // Arrange
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();
        var rol = RolBuilder.Nuevo().ConNombre("ADMIN").BuildNewDomain();

        // Act
        var agregado = usuario.AsignarRol(rol);

        // Assert
        Assert.That(agregado, Is.True);
        Assert.That(usuario.Roles.ToList(), Has.Count.EqualTo(1));
        Assert.That(usuario.TieneRol("ADMIN"), Is.True);
    }

    [Test]
    public void AsignarRol_Duplicado_NoAgregaSegundaVez()
    {
        // Arrange
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();
        var rol = RolBuilder.Nuevo().ConNombre("ADMIN").BuildNewDomain();
        usuario.AsignarRol(rol);

        // Act
        var agregado = usuario.AsignarRol(rol);

        // Assert
        Assert.That(agregado, Is.False);
        Assert.That(usuario.Roles.ToList(), Has.Count.EqualTo(1));
    }

    [Test]
    public void QuitarRol_RolAsignado_RetornaTrueYRemueveRol()
    {
        // Arrange
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();
        var rol = RolBuilder.Nuevo().ConNombre("ADMIN").BuildNewDomain();
        usuario.AsignarRol(rol);

        // Act
        var removido = usuario.QuitarRol(rol);

        // Assert
        Assert.That(removido, Is.True);
        Assert.That(usuario.Roles, Is.Empty);
    }

    [Test]
    public void QuitarRol_RolNoAsignado_RetornaFalse()
    {
        // Arrange
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();
        var rol = RolBuilder.Nuevo().ConNombre("ADMIN").BuildNewDomain();

        // Act
        var removido = usuario.QuitarRol(rol);

        // Assert
        Assert.That(removido, Is.False);
    }

    [Test]
    public void AsignarRoles_ListaDeRoles_ReemplazaRolesPrevios()
    {
        // Arrange
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();
        var admin = RolBuilder.Nuevo().ConNombre("ADMIN").BuildNewDomain();
        var ingeniero = RolBuilder.Nuevo().ConNombre("INGENIERO").BuildNewDomain();
        usuario.AsignarRol(admin);

        // Act
        usuario.AsignarRoles(new[] { ingeniero });

        // Assert
        Assert.That(usuario.Roles.ToList(), Has.Count.EqualTo(1));
        Assert.That(usuario.TieneRol("INGENIERO"), Is.True);
        Assert.That(usuario.TieneRol("ADMIN"), Is.False);
    }

    // =====================================================================
    // Activar / Desactivar
    // =====================================================================

    [Test]
    public void Desactivar_CambiaActivoAFalso()
    {
        var usuario = UsuarioBuilder.Nuevo().BuildNewDomain();

        usuario.Desactivar();

        Assert.That(usuario.Activo, Is.False);
    }

    [Test]
    public void Activar_CambiaActivoAVerdadero()
    {
        var usuario = UsuarioBuilder.Nuevo().Inactivo().BuildDomain();

        usuario.Activar();

        Assert.That(usuario.Activo, Is.True);
    }

    // =====================================================================
    // Reconstruccion
    // =====================================================================

    [Test]
    public void Reconstruir_DatosValidos_RetornaUsuarioConIdAsignado()
    {
        // Act
        var usuario = Usuario.Reconstruir(
            42,
            "Juan",
            "Perez",
            "juan@local",
            "jperez",
            "hash",
            true,
            "admin",
            DateTime.UtcNow);

        // Assert
        Assert.That(usuario.IdUsuario, Is.EqualTo(42));
        Assert.That(usuario.Nombres, Is.EqualTo("Juan"));
        Assert.That(usuario.Activo, Is.True);
    }
}
