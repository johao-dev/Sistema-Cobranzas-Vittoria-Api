using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using Cobranzas_Vittoria.Application.Common.Eventos;
using Cobranzas_Vittoria.Compras;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;
using Cobranzas_Vittoria.Compras.Contracts.Eventos;
using Cobranzas_Vittoria.Compras.Presentation.Dto;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Infrastructure.Eventos;
using Cobranzas_Vittoria.Notificaciones;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;
using Cobranzas_Vittoria.Notificaciones.Application.RequerimientoCreado;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.BackgroundJobs;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

[NonParallelizable]
public class EventosRequerimientoTests : IntegrationTestBase
{
    private int _idCoordinador;

    [SetUp]
    public async Task PrepararCatalogoYDestinatarios()
    {
        // Respawn limpia los catálogos de Notificaciones entre pruebas; se usa la migración real.
        Assembly assembly = typeof(Notificacion).Assembly;
        string recurso = assembly.GetManifestResourceNames().Single(nombre => nombre.EndsWith(
            "V3_0_2__Notificaciones_CatalogoInicialRequerimientoCreado.sql", StringComparison.Ordinal));
        using StreamReader lector = new(assembly.GetManifestResourceStream(recurso)!);
        string sql = await lector.ReadToEndAsync();
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();

        foreach (string lote in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(lote)) await db.ExecuteAsync(lote);
        }

        _idCoordinador = await db.QuerySingleAsync<int>("""
            INSERT INTO seguridad.Usuario (Nombres, Apellidos, Correo, UsuarioLogin, PasswordHash, Activo, FechaCreacion, UsuarioCreacion)
            OUTPUT INSERTED.IdUsuario
            VALUES (N'Coordinación', N'Prueba', 'coordinacion@example.com', 'eventos-prueba', 'no-utilizable', 1, SYSUTCDATETIME(), 'prueba');
            """);
        await db.ExecuteAsync("INSERT INTO seguridad.UsuarioRol (IdUsuario, IdRol, FechaCreacion) VALUES (@Id, 6, SYSUTCDATETIME());",
            new { Id = _idCoordinador });
    }

    [TearDown]
    public async Task LimpiarDestinatarioTemporal()
    {
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        await db.ExecuteAsync("""
            DROP TRIGGER IF EXISTS Notificaciones.TR_Prueba_FalloRegistroEvento;
            DELETE FROM seguridad.UsuarioRol WHERE IdUsuario = @Id;
            DELETE FROM seguridad.Usuario WHERE IdUsuario = @Id;
            """, new { Id = _idCoordinador });
    }

    [Test]
    public async Task Crear_PublicarResolverYPersistir_ConCommitPrevioContenidoYDestinatariosReales()
    {
        ObservadorCommit observador = new();
        using ServiceProvider services = CrearProvider(observador);
        using IServiceScope scope = services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<ProcesarEventoHandler>(), Is.Not.Null);
        Assert.That(scope.ServiceProvider.GetRequiredService<IOptions<NotificacionesWorkerOptions>>().Value.Habilitado, Is.False);
        Assert.That(scope.ServiceProvider.GetServices<IEventHandler<RequerimientoCreadoEvent>>().Last(),
            Is.TypeOf<RequerimientoCreadoEventHandler>());

        CrearRequerimientoResult resultado = await scope.ServiceProvider.GetRequiredService<CrearRequerimientoHandler>()
            .HandleAsync(Command());

        RequerimientoCreadoEvent evento = observador.Evento!;
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        long idNotificacion = await db.QuerySingleAsync<long>(
            "SELECT IdNotificacion FROM Notificaciones.Notificacion WHERE IdReferencia=@Id", new { Id = resultado.IdRequerimiento });
        Notificacion notificacion = (await scope.ServiceProvider.GetRequiredService<INotificacionRepository>()
            .ObtenerPorIdAsync(idNotificacion))!;
        string proyecto = await db.QuerySingleAsync<string>("SELECT NombreProyecto FROM maestra.Proyecto WHERE IdProyecto=@Id",
            new { Id = SeedIds.ProyectoMaytaCapacII });
        string solicitante = await db.QuerySingleAsync<string>(
            "SELECT LTRIM(RTRIM(CONCAT(Nombres, N' ', Apellidos))) FROM seguridad.Usuario WHERE IdUsuario=1");
        string correoAlmacen = await db.QuerySingleAsync<string>("SELECT Correo FROM seguridad.Usuario WHERE IdUsuario=3");

        Assert.Multiple(() =>
        {
            Assert.That(observador.ConfirmadoEnOtraConexion, Is.True);
            Assert.That(evento.IdRequerimiento, Is.EqualTo(resultado.IdRequerimiento));
            Assert.That(evento.NumeroRequerimiento, Is.EqualTo("EVENTO-REAL"));
            Assert.That(notificacion.IdEvento, Is.EqualTo(evento.IdEvento));
            Assert.That(notificacion.IdReferencia, Is.EqualTo(resultado.IdRequerimiento));
            Assert.That(notificacion.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Pendiente));
            Assert.That(notificacion.Intentos, Is.Zero);
            Assert.That(notificacion.AsuntoGenerado, Is.EqualTo("Nuevo requerimiento: EVENTO-REAL"));
            Assert.That(notificacion.CuerpoHtmlGenerado, Does.Contain(proyecto).And.Contain(solicitante)
                .And.Contain("10/11/2026").And.Contain("https://erp.example.com/sistema/requerimientos").And.Not.Contain("{{"));
            Assert.That(notificacion.Destinatarios.Select(d => (d.Correo, d.TipoEnvio)), Is.EquivalentTo(new[]
            {
                (correoAlmacen, NotificacionDestinatario.TipoEnvioConfig.To),
                ("coordinacion@example.com", NotificacionDestinatario.TipoEnvioConfig.Cc)
            }));
        });

        await scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(evento);
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Notificaciones.Notificacion WHERE IdEvento=@IdEvento",
            new { evento.IdEvento }), Is.EqualTo(1));
        Assert.That(await db.QuerySingleAsync<long>("SELECT IdNotificacion FROM Notificaciones.Notificacion WHERE IdEvento=@IdEvento",
            new { evento.IdEvento }), Is.EqualTo(idNotificacion));
    }

    [Test]
    public async Task CrearHttp_ConfirmaYGeneraPendiente_UsandoComposicionDeProgram()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/compras/requerimientos", Request());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        CrearRequerimientoResponse resultado = (await response.Content.ReadFromJsonAsync<CrearRequerimientoResponse>())!;

        Assert.That(await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM Notificaciones.Notificacion WHERE IdReferencia=@Id AND Estado='PENDIENTE'",
            new { Id = resultado.IdRequerimiento }), Is.EqualTo(1));
    }

    [Test]
    public async Task CrearHttp_FalloAlGuardarNotificacion_DevuelveExitoYConservaCompraSinRegistroParcial()
    {
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        await db.ExecuteAsync("""
            CREATE TRIGGER Notificaciones.TR_Prueba_FalloRegistroEvento ON Notificaciones.Notificacion
            AFTER INSERT AS THROW 51999, 'Fallo de persistencia simulado', 1;
            """);

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/compras/requerimientos", Request());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        CrearRequerimientoResponse resultado = (await response.Content.ReadFromJsonAsync<CrearRequerimientoResponse>())!;

        Assert.Multiple(() =>
        {
            Assert.That(resultado.IdRequerimiento, Is.GreaterThan(0));
        });
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM compras.Requerimiento WHERE IdRequerimiento=@Id",
            new { Id = resultado.IdRequerimiento }), Is.EqualTo(1));
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM compras.RequerimientoDetalle WHERE IdRequerimiento=@Id",
            new { Id = resultado.IdRequerimiento }), Is.EqualTo(1));
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Notificaciones.Notificacion"), Is.Zero);
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Notificaciones.NotificacionDestinatario"), Is.Zero);
    }

    [Test]
    public async Task CrearHttp_ValidacionFallida_NoPublicaNiPersisteNotificacion()
    {
        CrearRequerimientoRequest request = Request();
        request.IdProyecto = int.MaxValue;
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/compras/requerimientos", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM compras.Requerimiento"), Is.Zero);
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM Notificaciones.Notificacion"), Is.Zero);
    }

    [Test]
    public async Task Crear_CancelacionTrasCommit_ConservaResultadoYGeneraPendiente()
    {
        using CancellationTokenSource cts = new();
        ObservadorCommit observador = new() { DespuesDeConfirmar = cts.Cancel };
        using ServiceProvider services = CrearProvider(observador);
        using IServiceScope scope = services.CreateScope();

        CrearRequerimientoResult resultado = await scope.ServiceProvider.GetRequiredService<CrearRequerimientoHandler>()
            .HandleAsync(Command(), cts.Token);

        Assert.That(cts.IsCancellationRequested, Is.True);
        Assert.That(observador.TokenRecibido, Is.EqualTo(CancellationToken.None));
        Assert.That(await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM Notificaciones.Notificacion WHERE IdReferencia=@Id AND Estado='PENDIENTE'",
            new { Id = resultado.IdRequerimiento }), Is.EqualTo(1));
    }

    private static ServiceProvider CrearProvider(ObservadorCommit observador)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = GlobalSetupFixture.DbContainer.GetConnectionString(),
            ["Notificaciones:Enlaces:UrlBaseFrontend"] = "https://erp.example.com/sistema/",
            ["Notificaciones:Worker:Habilitado"] = "false"
        }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IUsuarioActualService, StubUsuarioActualService>();
        services.AddEventosEnMemoria();
        services.AddCompras();
        services.AddScoped<IEventHandler<RequerimientoCreadoEvent>>(_ => observador);
        services.AddNotificaciones(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static CrearRequerimientoRequest Request() => new()
    {
        NumeroRequerimiento = "EVENTO-REAL", FechaRequerimiento = new DateTime(2026, 11, 10),
        IdProyecto = SeedIds.ProyectoMaytaCapacII, Items = [new() { IdMaterial = 2, Cantidad = 1.25m }]
    };

    private static CrearRequerimientoCommand Command() => Request().ToCommand();

    private sealed class ObservadorCommit : IEventHandler<RequerimientoCreadoEvent>
    {
        public RequerimientoCreadoEvent? Evento { get; private set; }
        public bool ConfirmadoEnOtraConexion { get; private set; }
        public Action? DespuesDeConfirmar { get; init; }
        public CancellationToken TokenRecibido { get; private set; }

        public async Task HandleAsync(RequerimientoCreadoEvent evento, CancellationToken cancellationToken = default)
        {
            Evento = evento;
            TokenRecibido = cancellationToken;
            await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
            ConfirmadoEnOtraConexion = await db.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM compras.Requerimiento WHERE IdRequerimiento=@Id;
                """, new { Id = evento.IdRequerimiento }, cancellationToken: cancellationToken)) == 1;
            DespuesDeConfirmar?.Invoke();
        }
    }
}
