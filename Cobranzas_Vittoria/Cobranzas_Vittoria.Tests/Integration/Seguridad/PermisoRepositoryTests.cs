using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

/// <summary>
/// Pruebas de integracion de <see cref="PermisoRepository"/> contra la
/// base de datos efimera de Testcontainers.
///
/// Valida el mapeo entre <see cref="Permiso"/> y la tabla
/// <c>seguridad.Permiso</c>, asi como los stored procedures usados por
/// el CRUD. La BD se resetea antes de cada test mediante
/// <see cref="IntegrationTestBase"/>.
/// </summary>
public class PermisoRepositoryTests : IntegrationTestBase
{
    private PermisoRepository CrearRepository()
        => new(new TestConnectionFactory());

    [Test]
    public async Task AddAsync_ConDatosValidos_PersisteYRetornaEntidadConId()
    {
        // Arrange
        var repo = CrearRepository();
        var permiso = Permiso.Crear("repo.crear", "Crear desde repo", "Desc");
        permiso.EstablecerAuditoriaCreacion("admin");

        // Act
        var creado = await repo.AddAsync(permiso);

        // Assert
        Assert.That(creado.IdPermiso, Is.GreaterThan(0));
        Assert.That(creado.Codigo, Is.EqualTo("repo.crear"));
        Assert.That(creado.Nombre, Is.EqualTo("Crear desde repo"));
        Assert.That(creado.Activo, Is.True);
        Assert.That(creado.UsuarioCreacion, Is.EqualTo("admin"));
        Assert.That(creado.FechaCreacion, Is.Not.Null);
    }

    // NOTA: La validación de códigos duplicados se realiza en la capa de
    // aplicación (CreatePermisoHandler), no en el repositorio. Por eso no
    // hay test aquí que espere SqlException por UNIQUE violation.

    [Test]
    public async Task GetByIdAsync_Existente_RetornaPermiso()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearPermiso("repo.get", "Get"));

        // Act
        var obtenido = await repo.GetByIdAsync(creado.IdPermiso);

        // Assert
        Assert.That(obtenido, Is.Not.Null);
        Assert.That(obtenido!.Codigo, Is.EqualTo("repo.get"));
    }

    [Test]
    public async Task GetByIdAsync_Inexistente_RetornaNull()
    {
        var repo = CrearRepository();

        var obtenido = await repo.GetByIdAsync(999999);

        Assert.That(obtenido, Is.Null);
    }

    [Test]
    public async Task GetByCodigoAsync_Existente_RetornaPermiso()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearPermiso("repo.getbycode", "GetByCode"));

        // Act
        var obtenido = await repo.GetByCodigoAsync(creado.Codigo);

        // Assert
        Assert.That(obtenido, Is.Not.Null);
        Assert.That(obtenido!.Codigo, Is.EqualTo("repo.getbycode"));
    }

    [Test]
    public async Task GetByCodigoAsync_Inexistente_RetornaNull()
    {
        var repo = CrearRepository();

        var obtenido = await repo.GetByCodigoAsync("repo.inexistente");

        Assert.That(obtenido, Is.Null);
    }

    [Test]
    public async Task GetAllAsync_FiltraPorActivo()
    {
        // Arrange
        var repo = CrearRepository();
        var activo = await repo.AddAsync(CrearPermiso("repo.activo", "Activo"));
        var inactivo = await repo.AddAsync(CrearPermiso("repo.inactivo", "Inactivo"));
        inactivo.Desactivar();
        inactivo.EstablecerAuditoriaModificacion("admin");
        await repo.UpdateAsync(inactivo);

        // Act
        var activos = (await repo.GetAllAsync(activo: true)).ToList();
        var inactivos = (await repo.GetAllAsync(activo: false)).ToList();

        // Assert
        Assert.That(activos.Exists(p => p.IdPermiso == activo.IdPermiso), Is.True);
        Assert.That(activos.Exists(p => p.IdPermiso == inactivo.IdPermiso), Is.False);
        Assert.That(inactivos.Exists(p => p.IdPermiso == inactivo.IdPermiso), Is.True);
    }

    [Test]
    public async Task UpdateAsync_Existente_ActualizaNombreYDescripcion()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearPermiso("repo.update", "Original"));
        creado.ActualizarDatos("Actualizado", "Nueva desc");
        creado.EstablecerAuditoriaModificacion("admin");

        // Act
        var actualizado = await repo.UpdateAsync(creado);

        // Assert
        Assert.That(actualizado.Nombre, Is.EqualTo("Actualizado"));
        Assert.That(actualizado.Descripcion, Is.EqualTo("Nueva desc"));
        Assert.That(actualizado.UsuarioModificacion, Is.EqualTo("admin"));

        var enBd = await repo.GetByIdAsync(creado.IdPermiso);
        Assert.That(enBd!.Nombre, Is.EqualTo("Actualizado"));
    }

    [Test]
    public async Task DeleteAsync_Existente_EliminaFisicamente()
    {
        // Arrange
        var repo = CrearRepository();
        var creado = await repo.AddAsync(CrearPermiso("repo.delete", "Delete"));

        // Act
        await repo.DeleteAsync(creado.IdPermiso);

        // Assert
        var enBd = await repo.GetByIdAsync(creado.IdPermiso);
        Assert.That(enBd, Is.Null);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static Permiso CrearPermiso(string codigo, string nombre, string descripcion = "")
    {
        var permiso = Permiso.Crear(codigo, nombre, descripcion);
        permiso.EstablecerAuditoriaCreacion("test");
        return permiso;
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
