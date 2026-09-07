using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

/// <summary>
/// Pruebas de integracion de <see cref="UsuarioRepository"/> contra la
/// base de datos efimera de Testcontainers.
///
/// Valida el mapeo entre <see cref="Usuario"/> y la tabla
/// <c>seguridad.Usuario</c>, asi como los stored procedures usados por
/// el CRUD y la gestion de roles. La BD se resetea antes de cada test
/// mediante <see cref="IntegrationTestBase"/>.
/// </summary>
public class UsuarioRepositoryTests : IntegrationTestBase
{
    private UsuarioRepository CrearRepository()
        => new(new TestConnectionFactory());

    [Test]
    public async Task AddAsync_ConDatosValidos_PersisteYRetornaEntidadConId()
    {
        // Arrange
        var repo = CrearRepository();
        var usuario = CrearUsuario("repo.crear", "repo.crear@local");

        // Act
        var creado = await repo.AddAsync(usuario);

        // Assert
        Assert.That(creado.IdUsuario, Is.GreaterThan(0));
        Assert.That(creado.Nombres, Is.EqualTo("repo.crear"));
        Assert.That(creado.Apellidos, Is.EqualTo("Usuario"));
        Assert.That(creado.Correo.Value, Is.EqualTo("repo.crear@local"));
        Assert.That(creado.UsuarioLogin, Is.EqualTo("repo.crear"));
        Assert.That(creado.Activo, Is.True);
        Assert.That(creado.UsuarioCreacion, Is.EqualTo("test"));
        Assert.That(creado.FechaCreacion, Is.Not.Null);
    }

    [Test]
    public async Task GetByIdAsync_Existente_RetornaUsuario()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearUsuario("repo.get", "repo.get@local"));

        // Act
        var obtenido = await repo.GetByIdAsync(creado.IdUsuario);

        // Assert
        Assert.That(obtenido, Is.Not.Null);
        Assert.That(obtenido!.UsuarioLogin, Is.EqualTo("repo.get"));
    }

    [Test]
    public async Task GetByIdAsync_Inexistente_RetornaNull()
    {
        var repo = CrearRepository();

        var obtenido = await repo.GetByIdAsync(999999);

        Assert.That(obtenido, Is.Null);
    }

    [Test]
    public async Task GetByCorreoAsync_Existente_RetornaUsuario()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearUsuario("repo.getbycorreo", "repo.correo@local"));

        // Act
        var obtenido = await repo.GetByCorreoAsync(creado.Correo.Value);

        // Assert
        Assert.That(obtenido, Is.Not.Null);
        Assert.That(obtenido!.Correo.Value, Is.EqualTo("repo.correo@local"));
    }

    [Test]
    public async Task GetByCorreoAsync_Inexistente_RetornaNull()
    {
        var repo = CrearRepository();

        var obtenido = await repo.GetByCorreoAsync("repo.inexistente@local");

        Assert.That(obtenido, Is.Null);
    }

    [Test]
    public async Task GetAllAsync_FiltraPorActivo()
    {
        // Arrange
        var repo = CrearRepository();
        var activo = await repo.AddAsync(CrearUsuario("repo.activo", "repo.activo@local"));
        var inactivo = await repo.AddAsync(CrearUsuario("repo.inactivo", "repo.inactivo@local"));
        inactivo.Desactivar();
        await repo.UpdateAsync(inactivo);

        // Act
        var activos = (await repo.GetAllAsync(activo: true)).ToList();
        var inactivos = (await repo.GetAllAsync(activo: false)).ToList();
        var todos = (await repo.GetAllAsync(activo: null)).ToList();

        // Assert
        Assert.That(activos.Exists(u => u.IdUsuario == activo.IdUsuario), Is.True);
        Assert.That(activos.Exists(u => u.IdUsuario == inactivo.IdUsuario), Is.False);
        Assert.That(inactivos.Exists(u => u.IdUsuario == inactivo.IdUsuario), Is.True);
        Assert.That(todos.Exists(u => u.IdUsuario == activo.IdUsuario), Is.True);
        Assert.That(todos.Exists(u => u.IdUsuario == inactivo.IdUsuario), Is.True);
    }

    [Test]
    public async Task UpdateAsync_Existente_ActualizaNombreYApellidos()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearUsuario("repo.update", "repo.update@local"));
        creado.ActualizarDatos("Actualizado", "Lopez");

        // Act
        var actualizado = await repo.UpdateAsync(creado);

        // Assert
        Assert.That(actualizado.Nombres, Is.EqualTo("Actualizado"));
        Assert.That(actualizado.Apellidos, Is.EqualTo("Lopez"));

        var enBd = await repo.GetByIdAsync(creado.IdUsuario);
        Assert.That(enBd!.Nombres, Is.EqualTo("Actualizado"));
    }

    [Test]
    public async Task AsignarRolesAsync_DatosValidos_PersisteRelacion()
    {
        // Arrange
        var repo = CrearRepository();
        var usuario = await repo.AddAsync(CrearUsuario("repo.roles", "repo.roles@local"));

        // Act
        await repo.AsignarRolesAsync(usuario.IdUsuario, new[] { SeedIds.AdminId });

        // Assert
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.UsuarioRol WHERE IdUsuario = @idUsuario AND IdRol = @idRol",
            new { idUsuario = usuario.IdUsuario, idRol = SeedIds.AdminId });
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task QuitarRolAsync_DatosValidos_EliminaRelacion()
    {
        // Arrange
        var repo = CrearRepository();
        var usuario = await repo.AddAsync(CrearUsuario("repo.quitrol", "repo.quitrol@local"));
        await repo.AsignarRolesAsync(usuario.IdUsuario, new[] { SeedIds.AdminId });

        // Act
        await repo.QuitarRolAsync(usuario.IdUsuario, SeedIds.AdminId);

        // Assert
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.UsuarioRol WHERE IdUsuario = @idUsuario AND IdRol = @idRol",
            new { idUsuario = usuario.IdUsuario, idRol = SeedIds.AdminId });
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetByIdWithRolesAsync_ConRoles_RetornaUsuarioConRoles()
    {
        // Arrange
        var repo = CrearRepository();
        var usuario = await repo.AddAsync(CrearUsuario("repo.withroles", "repo.withroles@local"));
        await repo.AsignarRolesAsync(usuario.IdUsuario, new[] { SeedIds.AdminId });

        // Act
        var obtenido = await repo.GetByIdWithRolesAsync(usuario.IdUsuario);

        // Assert
        Assert.That(obtenido, Is.Not.Null);
        Assert.That(obtenido!.Roles.ToList(), Has.Count.EqualTo(1));
        Assert.That(obtenido.TieneRol("Administrador"), Is.True);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static Usuario CrearUsuario(string login, string correo)
    {
        var usuario = Usuario.Crear(
            login,
            "Usuario",
            correo,
            login,
            "password-hash",
            "test");
        return usuario;
    }

    /// <summary>
    /// Factory de conexiones que apunta al contenedor SQL de Testcontainers.
    /// </summary>
    private sealed class TestConnectionFactory : IDbConnectionFactory
    {
        public IDbConnection CreateConnection()
        {
            var cn = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
            cn.Open();
            return cn;
        }
    }
}
