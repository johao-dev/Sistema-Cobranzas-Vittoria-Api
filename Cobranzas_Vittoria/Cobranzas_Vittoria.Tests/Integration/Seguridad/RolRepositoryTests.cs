using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

/// <summary>
/// Pruebas de integracion de <see cref="RolRepository"/> contra la
/// base de datos efimera de Testcontainers.
///
/// Valida el mapeo entre <see cref="Rol"/> y la tabla
/// <c>seguridad.Rol</c>, asi como los stored procedures usados por
/// el CRUD. La BD se resetea antes de cada test mediante
/// <see cref="IntegrationTestBase"/>.
/// </summary>
public class RolRepositoryTests : IntegrationTestBase
{
    private RolRepository CrearRepository()
        => new(new TestConnectionFactory());

    [Test]
    public async Task AddAsync_ConDatosValidos_PersisteYRetornaEntidadConId()
    {
        // Arrange
        var repo = CrearRepository();
        var rol = Rol.Crear("Rol desde repo", "Desc");
        rol.EstablecerAuditoriaCreacion("admin");

        // Act
        var creado = await repo.AddAsync(rol);

        // Assert
        Assert.That(creado.IdRol, Is.GreaterThan(0));
        Assert.That(creado.Nombre, Is.EqualTo("Rol desde repo"));
        Assert.That(creado.Descripcion, Is.EqualTo("Desc"));
        Assert.That(creado.Activo, Is.True);
        Assert.That(creado.UsuarioCreacion, Is.EqualTo("admin"));
        Assert.That(creado.FechaCreacion, Is.Not.Null);
    }

    [Test]
    public async Task GetByIdAsync_Existente_RetornaRol()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearRol("repo.get", "Get"));

        // Act
        var obtenido = await repo.GetByIdAsync(creado.IdRol);

        // Assert
        Assert.That(obtenido, Is.Not.Null);
        Assert.That(obtenido!.Nombre, Is.EqualTo("repo.get"));
    }

    [Test]
    public async Task GetByIdAsync_Inexistente_RetornaNull()
    {
        var repo = CrearRepository();

        var obtenido = await repo.GetByIdAsync(999999);

        Assert.That(obtenido, Is.Null);
    }

    [Test]
    public async Task GetByNombreAsync_Existente_RetornaRol()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearRol("repo.getbyname", "GetByName"));

        // Act
        var obtenido = await repo.GetByNombreAsync(creado.Nombre);

        // Assert
        Assert.That(obtenido, Is.Not.Null);
        Assert.That(obtenido!.Nombre, Is.EqualTo("repo.getbyname"));
    }

    [Test]
    public async Task GetByNombreAsync_Inexistente_RetornaNull()
    {
        var repo = CrearRepository();

        var obtenido = await repo.GetByNombreAsync("repo.inexistente");

        Assert.That(obtenido, Is.Null);
    }

    [Test]
    public async Task GetAllAsync_FiltraPorActivo()
    {
        // Arrange
        var repo = CrearRepository();
        var activo = await repo.AddAsync(CrearRol("repo.activo", "Activo"));
        var inactivo = await repo.AddAsync(CrearRol("repo.inactivo", "Inactivo"));
        inactivo.Desactivar();
        inactivo.EstablecerAuditoriaModificacion("admin");
        await repo.UpdateAsync(inactivo);

        // Act
        var activos = (await repo.GetAllAsync(activo: true)).ToList();
        var inactivos = (await repo.GetAllAsync(activo: false)).ToList();
        var todos = (await repo.GetAllAsync(activo: null)).ToList();

        // Assert
        Assert.That(activos.Exists(r => r.IdRol == activo.IdRol), Is.True);
        Assert.That(activos.Exists(r => r.IdRol == inactivo.IdRol), Is.False);
        Assert.That(inactivos.Exists(r => r.IdRol == inactivo.IdRol), Is.True);
        Assert.That(todos.Exists(r => r.IdRol == activo.IdRol), Is.True);
        Assert.That(todos.Exists(r => r.IdRol == inactivo.IdRol), Is.True);
    }

    [Test]
    public async Task UpdateAsync_Existente_ActualizaNombreYDescripcion()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearRol("repo.update", "Original"));
        creado.ActualizarDatos("Actualizado", "Nueva desc");
        creado.EstablecerAuditoriaModificacion("admin");

        // Act
        var actualizado = await repo.UpdateAsync(creado);

        // Assert
        Assert.That(actualizado.Nombre, Is.EqualTo("Actualizado"));
        Assert.That(actualizado.Descripcion, Is.EqualTo("Nueva desc"));
        Assert.That(actualizado.UsuarioModificacion, Is.EqualTo("admin"));

        var enBd = await repo.GetByIdAsync(creado.IdRol);
        Assert.That(enBd!.Nombre, Is.EqualTo("Actualizado"));
    }

    [Test]
    public async Task DeleteAsync_Existente_EliminaFisicamente()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearRol("repo.delete", "Delete"));

        // Act
        await repo.DeleteAsync(creado.IdRol);

        // Assert
        var enBd = await repo.GetByIdAsync(creado.IdRol);
        Assert.That(enBd, Is.Null);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static Rol CrearRol(string nombre, string descripcion = "")
    {
        var rol = Rol.Crear(nombre, descripcion);
        rol.EstablecerAuditoriaCreacion("test");
        return rol;
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
