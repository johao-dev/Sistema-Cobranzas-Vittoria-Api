using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
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

    [TestCase(true)]
    [TestCase(false)]
    public async Task CambiarEstado_PersisteSoloEstadoYAuditoriaYConservaAsignaciones(bool habilitar)
    {
        PermisoRepository repo = CrearRepository();
        int idPermiso = await PermisoTestData.InsertarAsync($"repo.estado.{habilitar}", "Original", "Descripcion", !habilitar);
        int idOtro = await PermisoTestData.InsertarAsync($"repo.otro.{habilitar}", "Otro", activo: !habilitar);
        Permiso permiso = (await repo.GetByIdAsync(idPermiso))!;
        await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO seguridad.PermisoRol (IdPermiso, IdRol, FechaCreacion, UsuarioCreacion)
            VALUES (@idPermiso, 1, SYSUTCDATETIME(), 'test');
            SELECT @@ROWCOUNT;
            """, new { idPermiso });

        // El cambio de estado debe ignorar otros datos de una entidad modificada o desactualizada.
        permiso.ActualizarDatos("Nombre que no debe persistirse", "Descripcion que no debe persistirse");
        if (habilitar)
            permiso.Activar("admin-estado");
        else
            permiso.Desactivar("admin-estado");

        await PersistirEstadoAsync(repo, permiso, habilitar);

        Permiso enBd = (await repo.GetByIdAsync(idPermiso))!;
        Permiso otro = (await repo.GetByIdAsync(idOtro))!;
        int asignaciones = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.PermisoRol WHERE IdPermiso = @idPermiso AND IdRol = 1", new { idPermiso });

        Assert.Multiple(() =>
        {
            Assert.That(enBd.Activo, Is.EqualTo(habilitar));
            Assert.That(enBd.UsuarioModificacion, Is.EqualTo("admin-estado"));
            // La columna datetime2(0) redondea la auditoria al segundo mas cercano.
            Assert.That(enBd.FechaModificacion, Is.EqualTo(permiso.FechaModificacion).Within(TimeSpan.FromSeconds(0.5)));
            Assert.That(enBd.Codigo, Is.EqualTo(permiso.Codigo));
            Assert.That(enBd.Nombre, Is.EqualTo("Original"));
            Assert.That(enBd.Descripcion, Is.EqualTo("Descripcion"));
            Assert.That(enBd.FechaCreacion, Is.EqualTo(permiso.FechaCreacion));
            Assert.That(enBd.UsuarioCreacion, Is.EqualTo(permiso.UsuarioCreacion));
            Assert.That(asignaciones, Is.EqualTo(1));
            Assert.That(otro.Activo, Is.EqualTo(!habilitar));
            Assert.That(otro.FechaModificacion, Is.Null);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task UpdateAsync_EstadoCambiaDespuesDeLectura_NoSobrescribeEstado(bool habilitar)
    {
        PermisoRepository repo = CrearRepository();
        int idPermiso = await PermisoTestData.InsertarAsync($"repo.actualizacion.{habilitar}", "Original", activo: !habilitar);
        Permiso lecturaAnterior = (await repo.GetByIdAsync(idPermiso))!;
        Permiso cambioEstado = (await repo.GetByIdAsync(idPermiso))!;

        if (habilitar)
            cambioEstado.Activar("admin-estado");
        else
            cambioEstado.Desactivar("admin-estado");

        await PersistirEstadoAsync(repo, cambioEstado, habilitar);

        lecturaAnterior.ActualizarDatos("Actualizado", "Nueva descripcion");
        lecturaAnterior.EstablecerAuditoriaModificacion("editor");
        Permiso actualizado = await repo.UpdateAsync(lecturaAnterior);
        Permiso enBd = (await repo.GetByIdAsync(idPermiso))!;

        Assert.Multiple(() =>
        {
            Assert.That(actualizado.Activo, Is.EqualTo(habilitar));
            Assert.That(enBd.Activo, Is.EqualTo(habilitar));
            Assert.That(enBd.Nombre, Is.EqualTo("Actualizado"));
            Assert.That(enBd.Descripcion, Is.EqualTo("Nueva descripcion"));
            Assert.That(enBd.UsuarioModificacion, Is.EqualTo("editor"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CambiarEstado_PermisoInexistente_NoInformaExito(bool habilitar)
    {
        PermisoRepository repo = CrearRepository();
        Permiso permiso = Permiso.Reconstruir(int.MaxValue, "inexistente", "Nombre", "", habilitar);

        ValidacionNegocioSeguridadException? excepcion = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(
            () => PersistirEstadoAsync(repo, permiso, habilitar));

        Assert.That(excepcion!.Errores.Single().CodigoError, Is.EqualTo("PERMISO_NO_ENCONTRADO"));
    }

    private static Task PersistirEstadoAsync(PermisoRepository repo, Permiso permiso, bool habilitar)
        => habilitar ? repo.EnableAsync(permiso) : repo.DisableAsync(permiso);

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
