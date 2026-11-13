using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using Cobranzas_Vittoria.Application.Common.Eventos;
using Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion;
using Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.Aprobar;
using Cobranzas_Vittoria.ControlPresupuestario.Contracts.Eventos;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Repository;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Infrastructure.Eventos;
using Cobranzas_Vittoria.Notificaciones;
using Cobranzas_Vittoria.Notificaciones.Application.PresupuestoAprobado;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Dapper;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CP = Cobranzas_Vittoria.Seguridad.Authorization.Permisos.ControlPresupuestario;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

[NonParallelizable]
public class EventosPresupuestoTests : IntegrationTestBase
{
    private readonly List<int> _usuarios = new();

    [SetUp]
    public async Task PrepararCatalogosYCoordinadores()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            JwtTestTokenFactory.CrearToken(permisos: [CP.Version.Aprobar]));
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();

        // Respawn limpia estos catálogos; se reconstruyen con las migraciones reales.
        foreach (string nombre in new[]
        {
            "V3_0_2__Notificaciones_CatalogoInicialRequerimientoCreado.sql",
            "V3_0_5__Notificaciones_CatalogoPresupuestoAprobado.sql",
            "V3_0_6__Notificaciones_PresupuestoAprobado_MotivoCambio.sql"
        })
        {
            Assembly assembly = typeof(Notificacion).Assembly;
            string recurso = assembly.GetManifestResourceNames().Single(n => n.EndsWith(nombre, StringComparison.Ordinal));
            using StreamReader lector = new(assembly.GetManifestResourceStream(recurso)!);
            string sql = await lector.ReadToEndAsync();
            foreach (string lote in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(lote)) await db.ExecuteAsync(lote);
            }
        }

        await db.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.EstadoPresupuesto (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('BORRADOR'), ('APROBADO'), ('HISTORICO'), ('ANULADO')) v(Codigo);
            INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo, Nombre) VALUES ('PROYECTO', N'Proyecto');
            INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre) VALUES ('MATERIALES', N'Materiales');
            """);

        _usuarios.Clear();
        foreach (bool activo in new[] { true, false })
        {
            int id = await db.QuerySingleAsync<int>("""
                INSERT INTO seguridad.Usuario (Nombres, Correo, UsuarioLogin, PasswordHash, Activo)
                OUTPUT INSERTED.IdUsuario VALUES (N'Coordinación de prueba', @Correo, @Login, N'no-utilizable', @Activo);
                """, new
            {
                Correo = activo ? "coordinador-presupuesto@example.com" : "coordinador-inactivo@example.com",
                Login = "presupuesto-evento-" + Guid.NewGuid().ToString("N"), Activo = activo
            });
            _usuarios.Add(id);
            await db.ExecuteAsync("""
                INSERT INTO seguridad.UsuarioRol (IdUsuario, IdRol)
                SELECT @Id, IdRol FROM seguridad.Rol WHERE Nombre = N'Coordinador';
                """, new { Id = id });
        }
    }

    [TearDown]
    public async Task LimpiarUsuariosYTrigger()
    {
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        await db.ExecuteAsync("""
            DROP TRIGGER IF EXISTS Notificaciones.TR_Prueba_FalloPresupuesto;
            DELETE FROM seguridad.UsuarioRol WHERE IdUsuario IN @Ids;
            DELETE FROM seguridad.Usuario WHERE IdUsuario IN @Ids;
            """, new { Ids = _usuarios });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Aprobar_ConfirmaAntesDePublicarYGeneraTablaConCoordinadorGlobal(bool conProyecto)
    {
        VersionPrueba version = await CrearPresupuestoAsync(conProyecto);
        ObservadorCommit observador = new();
        using ServiceProvider services = CrearProvider(observador);
        using IServiceScope scope = services.CreateScope();
        Assert.That(scope.ServiceProvider.GetServices<IEventHandler<PresupuestoAprobadoEvent>>().Last(),
            Is.TypeOf<PresupuestoAprobadoEventHandler>());

        PresupuestoVersionResult respuesta = await scope.ServiceProvider.GetRequiredService<AprobarVersionHandler>()
            .HandleAsync(new(version.IdPresupuesto, version.IdPresupuestoVersion));

        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        Notificacion notificacion = await ObtenerNotificacionAsync(scope, version.IdPresupuestoVersion);
        DateTime fechaSql = await db.QuerySingleAsync<DateTime>(
            "SELECT FechaAprobacion FROM ControlPresupuestario.PresupuestoVersion WHERE IdPresupuestoVersion=@Id",
            new { Id = version.IdPresupuestoVersion });
        string proyecto = conProyecto
            ? await db.QuerySingleAsync<string>("SELECT NombreProyecto FROM maestra.Proyecto WHERE IdProyecto=@Id",
                new { Id = SeedIds.ProyectoMaytaCapacII })
            : "Sin proyecto asociado";
        string fecha = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fechaSql, DateTimeKind.Utc),
            TimeZoneInfo.FindSystemTimeZoneById("America/Lima")).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        HtmlDocument documento = new();
        documento.LoadHtml(notificacion.CuerpoHtmlGenerado);

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.Estado, Is.EqualTo("APROBADO"));
            Assert.That(observador.ConfirmadoEnOtraConexion, Is.True);
            Assert.That(observador.TokenRecibido, Is.EqualTo(CancellationToken.None));
            Assert.That(observador.Eventos.Single().FechaAprobacionUtc, Is.EqualTo(fechaSql));
            Assert.That(observador.Eventos.Single().FechaAprobacionUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(notificacion.IdEvento, Is.EqualTo(observador.Eventos.Single().IdEvento));
            Assert.That(notificacion.IdReferencia, Is.EqualTo(version.IdPresupuestoVersion));
            Assert.That(notificacion.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Pendiente));
            Assert.That(notificacion.AsuntoGenerado, Is.EqualTo("Presupuesto aprobado: PRE-EVENTO - versión 1"));
            Assert.That(documento.DocumentNode.SelectNodes("//table/tbody/tr"), Has.Count.EqualTo(5));
            Assert.That(notificacion.CuerpoHtmlGenerado, Does.Contain("Motivo de cambio:").And.Contain("No especificado"));
            Assert.That(notificacion.CuerpoHtmlGenerado, Does.Contain("Obra &lt;Norte&gt;").And.Contain(proyecto).And.Contain(fecha)
                .And.Not.Contain("href").And.Not.Contain("{{"));
            Assert.That(notificacion.Destinatarios.Select(d => (d.Correo, d.TipoEnvio)), Is.EquivalentTo(new[]
            {
                ("coordinador-presupuesto@example.com", NotificacionDestinatario.TipoEnvioConfig.To)
            }));
        });
    }

    [Test]
    public async Task Aprobar_CadaVersionGeneraSuNotificacion_YReentregarHistoricaNoDuplica()
    {
        VersionPrueba primera = await CrearPresupuestoAsync();
        ObservadorCommit observador = new();
        using ServiceProvider services = CrearProvider(observador);
        using IServiceScope scope = services.CreateScope();
        AprobarVersionHandler handler = scope.ServiceProvider.GetRequiredService<AprobarVersionHandler>();
        await handler.HandleAsync(new(primera.IdPresupuesto, primera.IdPresupuestoVersion));
        Notificacion original = await ObtenerNotificacionAsync(scope, primera.IdPresupuestoVersion);
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        VersionPrueba segunda = await db.QuerySingleAsync<VersionPrueba>("ControlPresupuestario.usp_PresupuestoVersion_CrearNueva",
            new { primera.IdPresupuesto, UsuarioCreacion = "test-user", MotivoCambio = "Ajuste <estructura>" },
            commandType: CommandType.StoredProcedure);

        await handler.HandleAsync(new(segunda.IdPresupuesto, segunda.IdPresupuestoVersion));
        await scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(observador.Eventos.First());

        Notificacion nueva = await ObtenerNotificacionAsync(scope, segunda.IdPresupuestoVersion);
        Assert.Multiple(() =>
        {
            Assert.That(observador.Eventos.Select(e => e.IdEvento).Distinct().Count(), Is.EqualTo(2));
            Assert.That(nueva.AsuntoGenerado, Does.EndWith("versión 2"));
            Assert.That(nueva.CuerpoHtmlGenerado, Does.Contain("Ajuste &lt;estructura&gt;"));
        });
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Notificaciones.Notificacion"), Is.EqualTo(2));
        Notificacion conservada = await ObtenerNotificacionAsync(scope, primera.IdPresupuestoVersion);
        Assert.That(conservada.IdNotificacion, Is.EqualTo(original.IdNotificacion));
        Assert.That(conservada.CuerpoHtmlGenerado, Is.EqualTo(original.CuerpoHtmlGenerado));
    }

    [TestCase("ReglaNotificacion")]
    [TestCase("PlantillaCorreo")]
    public async Task AprobarHttp_ConfiguracionDesactivada_ConservaAprobacionSinGenerar(string tabla)
    {
        VersionPrueba version = await CrearPresupuestoAsync();
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        // El nombre proviene exclusivamente de los casos de prueba.
        await db.ExecuteAsync($"""
            UPDATE Notificaciones.{tabla} SET Activo=0 WHERE IdCodigoEventoNotificacion=
                (SELECT IdCodigoEvento FROM Notificaciones.CodigoEventoNotificacion WHERE Codigo=N'PRESUPUESTO_APROBADO');
            """);

        HttpResponseMessage response = await _client.PostAsync(RutaAprobar(version), null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(await ContarNotificacionesAsync(), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AprobarHttp_FallaNotificacion_ConservaAprobacionSinRegistroParcial(bool sinDestinatarios)
    {
        VersionPrueba version = await CrearPresupuestoAsync();
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        if (sinDestinatarios)
        {
            await db.ExecuteAsync("UPDATE seguridad.Usuario SET Activo=0 WHERE IdUsuario IN @Ids", new { Ids = _usuarios });
        }
        else
        {
            await db.ExecuteAsync("""
                CREATE TRIGGER Notificaciones.TR_Prueba_FalloPresupuesto ON Notificaciones.Notificacion
                AFTER INSERT AS THROW 51999, 'Fallo de persistencia simulado', 1;
                """);
        }

        HttpResponseMessage response = await _client.PostAsync(RutaAprobar(version), null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(await db.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoVersion v
            JOIN ControlPresupuestario.EstadoPresupuesto e ON e.IdEstadoPresupuesto=v.IdEstadoPresupuesto
            WHERE v.IdPresupuestoVersion=@Id AND e.Codigo='APROBADO' AND v.FechaAprobacion IS NOT NULL;
            """, new { Id = version.IdPresupuestoVersion }), Is.EqualTo(1));
        Assert.That(await ContarNotificacionesAsync(), Is.Zero);
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Notificaciones.NotificacionDestinatario"), Is.Zero);
    }

    [Test]
    public async Task AprobarHttp_SinDetalles_NoApruebaNiGeneraNotificacion()
    {
        VersionPrueba version = await CrearPresupuestoAsync();
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        await db.ExecuteAsync("DELETE FROM ControlPresupuestario.PresupuestoDetalle WHERE IdPresupuestoVersion=@Id",
            new { Id = version.IdPresupuestoVersion });

        HttpResponseMessage response = await _client.PostAsync(RutaAprobar(version), null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await ContarNotificacionesAsync(), Is.Zero);
        Assert.That(await db.ExecuteScalarAsync<DateTime?>(
            "SELECT FechaAprobacion FROM ControlPresupuestario.PresupuestoVersion WHERE IdPresupuestoVersion=@Id",
            new { Id = version.IdPresupuestoVersion }), Is.Null);
    }

    [Test]
    [Repeat(10)]
    public async Task AprobarHttp_Concurrente_UnaAprobacionYUnaNotificacion()
    {
        VersionPrueba version = await CrearPresupuestoAsync();

        HttpResponseMessage[] respuestas = await Task.WhenAll(
            _client.PostAsync(RutaAprobar(version), null), _client.PostAsync(RutaAprobar(version), null));
        string[] cuerpos = await Task.WhenAll(respuestas.Select(r => r.Content.ReadAsStringAsync()));

        Assert.That(respuestas.Select(r => r.StatusCode), Is.EquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }),
            string.Join(Environment.NewLine, cuerpos));
        Assert.That(await ContarNotificacionesAsync(), Is.EqualTo(1));
    }

    private static string RutaAprobar(VersionPrueba version)
        => $"/api/control-presupuestario/presupuestos/{version.IdPresupuesto}/versiones/{version.IdPresupuestoVersion}/aprobar";

    private static Task<int> ContarNotificacionesAsync()
        => DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM Notificaciones.Notificacion");

    private static async Task<Notificacion> ObtenerNotificacionAsync(IServiceScope scope, int idVersion)
    {
        long id = await DbHelpers.QueryScalarAsync<long>(
            "SELECT IdNotificacion FROM Notificaciones.Notificacion WHERE IdReferencia=@Id", new { Id = idVersion });
        return (await scope.ServiceProvider.GetRequiredService<INotificacionRepository>().ObtenerPorIdAsync(id))!;
    }

    private static ServiceProvider CrearProvider(ObservadorCommit observador)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = GlobalSetupFixture.DbContainer.GetConnectionString(),
            ["Notificaciones:Worker:Habilitado"] = "false"
        }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IUsuarioActualService, StubUsuarioActualService>();
        services.AddScoped<IPresupuestoVersionRepository, PresupuestoVersionRepository>();
        services.AddScoped<AprobarVersionHandler>();
        services.AddEventosEnMemoria();
        services.AddScoped<IEventHandler<PresupuestoAprobadoEvent>>(_ => observador);
        services.AddNotificaciones(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static async Task<VersionPrueba> CrearPresupuestoAsync(bool conProyecto = true)
    {
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        int centro = await db.QuerySingleAsync<int>("""
            INSERT INTO ControlPresupuestario.CentroCosto (Codigo, Nombre, IdTipoCentroCosto, IdProyecto)
            OUTPUT INSERTED.IdCentroCosto
            VALUES ('CC-EVENTO', N'Centro de prueba',
                (SELECT IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo='PROYECTO'), @IdProyecto);
            """, new { IdProyecto = conProyecto ? (int?)SeedIds.ProyectoMaytaCapacII : null });
        int moneda = await DbHelpersMoneda.ObtenerPenAsync();
        VersionPrueba version = await db.QuerySingleAsync<VersionPrueba>("ControlPresupuestario.usp_Presupuesto_Crear",
            new { IdCentroCosto = centro, IdMoneda = moneda, Codigo = "PRE-EVENTO", Nombre = "Obra <Norte>", UsuarioCreacion = "test-user" },
            commandType: CommandType.StoredProcedure);
        await db.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.CatalogoPartida (Codigo, Nombre, IdTipoPartida, Nivel)
            VALUES ('MAT-EVENTO', N'Materiales',
                (SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo='MATERIALES'), 1);
            DECLARE @IdPartida INT = CONVERT(INT, SCOPE_IDENTITY());
            INSERT INTO ControlPresupuestario.PresupuestoDetalle (IdPresupuestoVersion, IdCatalogoPartida, MontoPresupuestado)
            VALUES (@IdVersion, @IdPartida, 1000);
            """, new { IdVersion = version.IdPresupuestoVersion });
        return version;
    }

    private sealed class VersionPrueba
    {
        public int IdPresupuesto { get; set; }
        public int IdPresupuestoVersion { get; set; }
    }

    private sealed class ObservadorCommit : IEventHandler<PresupuestoAprobadoEvent>
    {
        public List<PresupuestoAprobadoEvent> Eventos { get; } = new();
        public bool ConfirmadoEnOtraConexion { get; private set; }
        public CancellationToken TokenRecibido { get; private set; }

        public async Task HandleAsync(PresupuestoAprobadoEvent evento, CancellationToken cancellationToken = default)
        {
            // Reentregar la misma ocurrencia no representa una nueva aprobación.
            if (Eventos.All(e => e.IdEvento != evento.IdEvento)) Eventos.Add(evento);
            TokenRecibido = cancellationToken;
            ConfirmadoEnOtraConexion = await DbHelpers.QueryScalarAsync<int>("""
                SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoVersion
                WHERE IdPresupuestoVersion=@Id AND FechaAprobacion=@Fecha;
                """, new { Id = evento.IdPresupuestoVersion, Fecha = evento.FechaAprobacionUtc }) == 1;
        }
    }
}
