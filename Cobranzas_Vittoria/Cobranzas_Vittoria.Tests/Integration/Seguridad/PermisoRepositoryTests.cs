using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

/// <summary>
/// Pruebas de integracion de <see cref="PermisoRepository"/> contra la
/// base de datos efimera de Testcontainers.
///
/// Valida el mapeo entre <see cref="Permiso"/> y la tabla
/// <c>seguridad.Permiso</c>, asi como los stored procedures usados por
/// las consultas y la actualizacion. Los escenarios se preparan directamente
/// en la base de datos sin depender de operaciones de alta del repositorio.
/// </summary>
public class PermisoRepositoryTests : IntegrationTestBase
{
    private PermisoRepository CrearRepository()
        => new(new TestConnectionFactory());

    [Test]
    public async Task GetByIdAsync_Existente_RetornaPermiso()
    {
        // Arrange
        var repo = CrearRepository();
        int idPermiso = await PermisoTestData.InsertarAsync("repo.get", "Get");

        // Act
        Permiso? obtenido = await repo.GetByIdAsync(idPermiso);

        // Assert
        Assert.That(obtenido, Is.Not.Null);
        Assert.That(obtenido!.IdPermiso, Is.EqualTo(idPermiso));
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
    public async Task GetAllAsync_FiltraPorActivo()
    {
        // Arrange
        var repo = CrearRepository();
        int idActivo = await PermisoTestData.InsertarAsync("repo.activo", "Activo");
        int idInactivo = await PermisoTestData.InsertarAsync("repo.inactivo", "Inactivo", activo: false);

        // Act
        List<Permiso> activos = (await repo.GetAllAsync(activo: true)).ToList();
        List<Permiso> inactivos = (await repo.GetAllAsync(activo: false)).ToList();

        // Assert
        Assert.That(activos.Exists(p => p.IdPermiso == idActivo), Is.True);
        Assert.That(activos.Exists(p => p.IdPermiso == idInactivo), Is.False);
        Assert.That(inactivos.Exists(p => p.IdPermiso == idInactivo), Is.True);
    }

    [Test]
    public async Task UpdateAsync_Existente_ActualizaNombreYDescripcion()
    {
        // Arrange
        var repo = CrearRepository();
        int idPermiso = await PermisoTestData.InsertarAsync("repo.update", "Original");
        Permiso creado = (await repo.GetByIdAsync(idPermiso))!;

        creado.ActualizarDatos("Actualizado", "Nueva desc");
        creado.EstablecerAuditoriaModificacion("admin");

        // Act
        Permiso actualizado = await repo.UpdateAsync(creado);

        // Assert
        Assert.That(actualizado.Nombre, Is.EqualTo("Actualizado"));
        Assert.That(actualizado.Descripcion, Is.EqualTo("Nueva desc"));
        Assert.That(actualizado.UsuarioModificacion, Is.EqualTo("admin"));

        Permiso? enBd = await repo.GetByIdAsync(idPermiso);
        Assert.That(enBd!.Nombre, Is.EqualTo("Actualizado"));
    }

    // =====================================================================
    // Helpers
    // =====================================================================

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
