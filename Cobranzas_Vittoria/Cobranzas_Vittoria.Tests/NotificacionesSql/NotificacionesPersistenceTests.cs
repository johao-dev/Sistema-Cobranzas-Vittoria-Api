using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Application.Options;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarNotificacionesPendientes;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Email;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Plantillas;
using Cobranzas_Vittoria.Tests.Unit.Notificaciones.Infrastructure;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Destinatarios;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Persistence.Repository;
using Dapper;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using Testcontainers.MsSql;

// Fuera del SetUpFixture de Integration: solo requiere Seguridad y Notificaciones, nunca datos reales.
namespace Cobranzas_Vittoria.Tests.NotificacionesSql;

[TestFixture(false)]
[TestFixture(true)]
[Category("Integration")]
[NonParallelizable]
public class NotificacionesPersistenceTests(bool rcsi)
{
    private MsSqlContainer _container = null!;
    private ConexionPrueba _factory = null!;
    private NotificacionRepository _repository = null!;
    private ConfiguracionNotificacionRepository _configuracion = null!;
    private static readonly DateTime AHORA = new(2026, 11, 10, 18, 0, 0, DateTimeKind.Utc);

    [OneTimeSetUp]
    public async Task IniciarSqlAislado()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword("NotificacionesTest123#").Build();
        await _container.StartAsync();
        SqlConnectionStringBuilder conexion = new(_container.GetConnectionString());
        await using (SqlConnection master = new(conexion.ConnectionString))
        {
            await master.OpenAsync();
            await master.ExecuteAsync("CREATE DATABASE NotificacionesPruebas;");
            if (rcsi)
            {
                await master.ExecuteAsync("ALTER DATABASE NotificacionesPruebas SET READ_COMMITTED_SNAPSHOT ON;");
            }
        }

        conexion.InitialCatalog = "NotificacionesPruebas";
        _factory = new ConexionPrueba(conexion.ConnectionString);
        _repository = new NotificacionRepository(_factory);
        _configuracion = new ConfiguracionNotificacionRepository(_factory);

        using IDbConnection db = _factory.CreateConnection();
        await db.ExecuteAsync("CREATE SCHEMA seguridad;");
        string[] migraciones = ["V1_0_2__Seguridad_DDL.sql", "V1_3_0__Seguridad_Actualiza_Tabla_Rol.sql",
            "V3_0_0__Notificaciones_CreaSchema.sql",
            "V3_0_1__Notificaciones_CreaTablas.sql", "V3_0_2__Notificaciones_CatalogoInicialRequerimientoCreado.sql"];

        foreach (string nombre in migraciones)
        {
            await EjecutarMigracionAsync(db, nombre);
        }
    }

    private static async Task EjecutarMigracionAsync(IDbConnection db, string nombre)
    {
        Assembly assembly = typeof(Notificacion).Assembly;
        string recurso = assembly.GetManifestResourceNames().Single(n => n.EndsWith(nombre, StringComparison.Ordinal));
        using StreamReader lector = new(assembly.GetManifestResourceStream(recurso)!);
        string sql = await lector.ReadToEndAsync();

        foreach (string lote in Regex.Split(sql, @"^\s*GO\s*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(lote)) await db.ExecuteAsync(lote);
        }
    }

    [OneTimeTearDown]
    public async Task DetenerSql()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [SetUp]
    public async Task LimpiarCola()
    {
        using IDbConnection db = _factory.CreateConnection();
        await db.ExecuteAsync("""
            DELETE FROM Notificaciones.NotificacionDestinatario;
            DELETE FROM Notificaciones.Notificacion;
            DELETE FROM seguridad.UsuarioRol;
            DELETE FROM seguridad.Usuario;
            DELETE FROM seguridad.Rol;
            """);
    }

    [Test]
    public async Task RegistroAtomico_ReconstruyeColeccionesUtcYConservaInstantaneaDuplicada()
    {
        Notificacion nueva = Nueva();
        ResultadoRegistroNotificacion primera = await _repository.RegistrarSiNoExisteAsync(nueva);
        ResultadoRegistroNotificacion duplicada = await _repository.RegistrarSiNoExisteAsync(Nueva(nueva.IdEvento, "Otro asunto"));
        Notificacion guardada = (await _repository.ObtenerPorIdAsync(primera.IdNotificacion))!;

        Assert.Multiple(() =>
        {
            Assert.That(primera.Creada, Is.True);
            Assert.That(duplicada, Is.EqualTo(new ResultadoRegistroNotificacion(primera.IdNotificacion, false)));
            Assert.That(guardada.AsuntoGenerado, Is.EqualTo(nueva.AsuntoGenerado));
            Assert.That(guardada.FechaCreacion.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(guardada.Destinatarios.Count, Is.EqualTo(3));
            Assert.That(guardada.Destinatarios.All(d => d.IdNotificacion == primera.IdNotificacion && d.IdNotificacionDestinatario > 0), Is.True);
            Assert.That(guardada.Destinatarios.Select(d => d.TipoEnvio), Is.EquivalentTo(Enum.GetValues<NotificacionDestinatario.TipoEnvioConfig>()));
        });
        Assert.That(await _repository.ObtenerPorIdAsync(long.MaxValue), Is.Null);
    }

    [Test]
    public async Task DosAltasConcurrentes_DevuelvenUnUnicoRegistro()
    {
        Guid id = Guid.NewGuid();
        ResultadoRegistroNotificacion[] registros = await Task.WhenAll(
            _repository.RegistrarSiNoExisteAsync(Nueva(id)), _repository.RegistrarSiNoExisteAsync(Nueva(id)));

        Assert.That(registros.Select(r => r.IdNotificacion).Distinct().Count(), Is.EqualTo(1));
        Assert.That(registros.Count(r => r.Creada), Is.EqualTo(1));
    }

    [Test]
    public async Task ErrorEnDestinatario_RevierteCabeceraYTodosLosDestinatarios()
    {
        Notificacion origen = Nueva();
        Notificacion invalida = Notificacion.Reconstruir(0, origen.IdEvento, 1, 1, "Asunto", "<p>Correo</p>",
            Notificacion.EstadoNotificacion.Pendiente, AHORA, null, null, null, 0, null,
            [origen.Destinatarios.First(), NotificacionDestinatario.Reconstruir(0, 0, new string('a', 255), null,
                NotificacionDestinatario.TipoEnvioConfig.To)]);

        Assert.ThrowsAsync<SqlException>(async () => await _repository.RegistrarSiNoExisteAsync(invalida));
        using IDbConnection db = _factory.CreateConnection();
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Notificaciones.Notificacion"), Is.Zero);
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Notificaciones.NotificacionDestinatario"), Is.Zero);
    }

    [Test]
    public void OtraViolacionIntegridad_NoSeConvierteEnDuplicado()
    {
        Notificacion invalida = Notificacion.Crear(Guid.NewGuid(), int.MaxValue, 1, "Asunto", "<p>Correo</p>", AHORA,
            Nueva().Destinatarios);
        SqlException? error = Assert.ThrowsAsync<SqlException>(async () => await _repository.RegistrarSiNoExisteAsync(invalida));
        Assert.That(error!.Number, Is.EqualTo(547));
    }

    [Test]
    public async Task ReclamacionConcurrente_IncrementaUnaVezYRespetaReintentos()
    {
        for (int i = 0; i < 9; i++) await _repository.RegistrarSiNoExisteAsync(Nueva());
        long futura = (await _repository.RegistrarSiNoExisteAsync(Nueva())).IdNotificacion;
        using (IDbConnection db = _factory.CreateConnection())
        {
            await db.ExecuteAsync("UPDATE Notificaciones.Notificacion SET FechaProximoIntento=@Fecha WHERE IdNotificacion=@Id",
                new { Fecha = AHORA.AddMinutes(1), Id = futura });
        }

        IReadOnlyCollection<Notificacion>[] lotes = await Task.WhenAll(
            _repository.ReclamarPendientesAsync(5, AHORA), _repository.ReclamarPendientesAsync(5, AHORA));
        List<Notificacion> reclamadas = lotes.SelectMany(l => l).ToList();

        // READPAST puede omitir candidatas bloqueadas por el otro recorrido, incluso si este
        // finalmente no las reclama. El ciclo siguiente debe recogerlas sin duplicar intentos.
        reclamadas.AddRange(await _repository.ReclamarPendientesAsync(5, AHORA));

        Assert.Multiple(() =>
        {
            Assert.That(reclamadas.Count, Is.EqualTo(9));
            Assert.That(reclamadas.Select(n => n.IdNotificacion).Distinct().Count(), Is.EqualTo(9));
            Assert.That(reclamadas.All(n => n.Estado == Notificacion.EstadoNotificacion.Procesando && n.Intentos == 1
                && n.FechaInicioProcesamiento == AHORA && n.FechaInicioProcesamiento.Value.Kind == DateTimeKind.Utc
                && n.FechaProximoIntento is null && n.Destinatarios.Count == 3), Is.True);
        });
        Assert.That(await _repository.ReclamarPendientesAsync(3, AHORA), Is.Empty);
        Assert.That((await _repository.ReclamarPendientesAsync(3, AHORA.AddMinutes(1))).Single().IdNotificacion, Is.EqualTo(futura));
    }

    [TestCase("enviada")]
    [TestCase("reintento")]
    [TestCase("error")]
    public async Task GuardadoCondicional_ConservaContenidoYRechazaResultadoAnterior(string resultado)
    {
        long id = (await _repository.RegistrarSiNoExisteAsync(Nueva())).IdNotificacion;
        Notificacion notificacion = (await _repository.ReclamarPendientesAsync(3, AHORA)).Single();
        if (resultado == "enviada") notificacion.MarcarComoEnviada(AHORA);
        else notificacion.RegistrarFallo("Diagnóstico seguro", AHORA, 3, TimeSpan.FromMinutes(5), resultado != "error");

        Assert.That(await _repository.GuardarResultadoAsync(notificacion, 1), Is.True);
        Assert.That(await _repository.GuardarResultadoAsync(notificacion, 1), Is.False);
        Notificacion guardada = (await _repository.ObtenerPorIdAsync(id))!;
        Assert.That(guardada.Estado, Is.EqualTo(notificacion.Estado));
        Assert.That(guardada.Intentos, Is.EqualTo(1));
        Assert.That(guardada.Destinatarios.Count, Is.EqualTo(3));

        if (resultado == "reintento")
        {
            Assert.That(await _repository.ReclamarPendientesAsync(3, AHORA.AddMinutes(4)), Is.Empty);
            Notificacion segundo = (await _repository.ReclamarPendientesAsync(3, AHORA.AddMinutes(5))).Single();
            Assert.That(segundo.Intentos, Is.EqualTo(2));
            Assert.That(await _repository.GuardarResultadoAsync(notificacion, 1), Is.False);
        }
    }

    [Test]
    public async Task Recuperacion_SoloLeeLoteVencidoConLimiteEstricto()
    {
        for (int i = 0; i < 4; i++) await _repository.RegistrarSiNoExisteAsync(Nueva());
        await _repository.ReclamarPendientesAsync(4, AHORA.AddMinutes(-11));
        Assert.That(await _repository.ObtenerProcesamientosVencidosAsync(3, AHORA.AddMinutes(-11)), Is.Empty);
        IReadOnlyCollection<Notificacion> vencidas = await _repository.ObtenerProcesamientosVencidosAsync(3, AHORA.AddMinutes(-10));
        Assert.That(vencidas.Count, Is.EqualTo(3));
        Notificacion original = (await _repository.ObtenerPorIdAsync(vencidas.First().IdNotificacion))!;
        Assert.That(original.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Procesando));
        Assert.That(original.Intentos, Is.EqualTo(1));
    }

    [Test]
    public async Task DosResultadosConcurrentes_SoloUnoSeGuardaSinAlterarContenidoNiDestinatarios()
    {
        await _repository.RegistrarSiNoExisteAsync(Nueva());
        Notificacion intento = (await _repository.ReclamarPendientesAsync(3, AHORA)).Single();
        Notificacion resultado = Notificacion.Reconstruir(intento.IdNotificacion, intento.IdEvento,
            intento.IdCodigoEventoNotificacion, intento.IdReferencia, "Contenido que no debe guardarse", "<p>Otro</p>",
            Notificacion.EstadoNotificacion.Enviada, AHORA.AddYears(1), null, null, AHORA, 1, null, []);

        bool[] guardadas = await Task.WhenAll(_repository.GuardarResultadoAsync(resultado, 1),
            _repository.GuardarResultadoAsync(resultado, 1));
        Assert.That(guardadas.Count(g => g), Is.EqualTo(1));
        Notificacion final = (await _repository.ObtenerPorIdAsync(intento.IdNotificacion))!;
        Assert.That(final.AsuntoGenerado, Is.EqualTo(intento.AsuntoGenerado));
        Assert.That(final.CuerpoHtmlGenerado, Is.EqualTo(intento.CuerpoHtmlGenerado));
        Assert.That(final.FechaCreacion, Is.EqualTo(intento.FechaCreacion));
        Assert.That(final.Destinatarios.Count, Is.EqualTo(3));
        Assert.That(final.Intentos, Is.EqualTo(1));
        Assert.ThrowsAsync<ArgumentException>(async () => await _repository.GuardarResultadoAsync(intento, 1));
        Assert.ThrowsAsync<ArgumentException>(async () => await _repository.GuardarResultadoAsync(resultado, 2));
    }

    [Test]
    public async Task PlantillaInicialReal_SeRenderizaSinCambiarLaMigracion()
    {
        ConfiguracionNotificacion configuracion = (await _configuracion.ObtenerPorCodigoEventoAsync("REQUERIMIENTO_CREADO"))!;
        ContenidoCorreoRenderizado contenido = await new PlantillaRenderer().RenderizarAsync(configuracion.CodigoEvento,
            configuracion.Plantilla!, new Dictionary<string, string?>
            {
                ["NumeroRequerimiento"] = "REQ-125", ["Proyecto"] = "Proyecto <Norte>",
                ["Solicitante"] = "Usuario", ["Fecha"] = "10/11/2026", ["Url"] = "https://erp.example.com/r/125"
            });
        Assert.That(contenido.Asunto, Is.EqualTo("Nuevo requerimiento: REQ-125"));
        Assert.That(contenido.CuerpoHtml, Does.Contain("Proyecto &lt;Norte&gt;"));
    }

    [Test]
    public async Task CatalogoPresupuestoAprobado_ConfiguraCoordinadorGlobalSinCopiasYPlantillaValida()
    {
        using IDbConnection db = _factory.CreateConnection();
        // El ID difiere del 6 usado en el catálogo inicial de requerimientos.
        await db.ExecuteAsync("""
            SET IDENTITY_INSERT seguridad.Rol ON;
            INSERT INTO seguridad.Rol (IdRol, Nombre) VALUES (42, N'Coordinador');
            SET IDENTITY_INSERT seguridad.Rol OFF;
            """);

        await EjecutarMigracionAsync(db, "V3_0_5__Notificaciones_CatalogoPresupuestoAprobado.sql");
        await EjecutarMigracionAsync(db, "V3_0_6__Notificaciones_PresupuestoAprobado_MotivoCambio.sql");

        ConfiguracionNotificacion configuracion = (await _configuracion.ObtenerPorCodigoEventoAsync("PRESUPUESTO_APROBADO"))!;
        ReglaDestinatario destinatario = configuracion.Regla!.Destinatarios.Single();
        ContenidoCorreoRenderizado contenido = await new PlantillaRenderer().RenderizarAsync(configuracion.CodigoEvento,
            configuracion.Plantilla!, new Dictionary<string, string?>
            {
                ["CodigoPresupuesto"] = "PRE-001", ["NombrePresupuesto"] = "Obra <Norte>",
                ["NumeroVersion"] = "2", ["Proyecto"] = "Proyecto <Norte>",
                ["Fecha"] = "13/11/2026", ["MotivoCambio"] = "Ajuste <estructura>"
            });

        Assert.Multiple(() =>
        {
            Assert.That(configuracion.Regla.Activo, Is.True);
            Assert.That(configuracion.Plantilla!.Activo, Is.True);
            Assert.That(destinatario.TipoDestinatario, Is.EqualTo(TipoDestinatario.Rol));
            Assert.That(destinatario.Valor, Is.EqualTo("42"));
            Assert.That(destinatario.TipoEnvio, Is.EqualTo(NotificacionDestinatario.TipoEnvioConfig.To));
            Assert.That(contenido.Asunto, Is.EqualTo("Presupuesto aprobado: PRE-001 - versión 2"));
            Assert.That(contenido.CuerpoHtml, Does.Contain("Obra &lt;Norte&gt;").And.Contain("Proyecto &lt;Norte&gt;")
                .And.Contain("Ajuste &lt;estructura&gt;"));
            Assert.That(contenido.CuerpoHtml, Does.Not.Contain("{{"));
        });
    }

    [Test]
    public async Task FlujoAislado_EventoPersisteProcesaSmtpYFinalizaEnviada()
    {
        (int evento, _) = await CrearEvento();
        using IDbConnection db = _factory.CreateConnection();
        int tipo = await db.QuerySingleAsync<int>("SELECT IdTipoDestinatario FROM Notificaciones.TipoDestinatario WHERE Nombre=N'USUARIO'");
        int usuario = await db.QuerySingleAsync<int>("""
            INSERT INTO seguridad.Usuario (Nombres, Correo, UsuarioLogin, PasswordHash)
            OUTPUT INSERTED.IdUsuario VALUES (N'Prueba', N'aislado@example.com', N'aislado', N'hash-ficticio');
            """);
        ReglaNotificacion regla = ReglaNotificacion.Crear(evento, "Aislada");
        regla.AgregarDestinatario(ReglaDestinatario.Crear(tipo, TipoDestinatario.Usuario,
            usuario.ToString(), NotificacionDestinatario.TipoEnvioConfig.To));
        await _configuracion.GuardarReglaAsync(regla);
        await _configuracion.GuardarPlantillaAsync(PlantillaCorreo.Crear(evento, "Prueba {{Dato}}", "<p>{{Dato}}</p>"));
        ProcesarEventoHandler generar = new(_configuracion, _repository,
            new DestinatarioResolver([new DestinatarioRolResolver(_factory), new DestinatarioUsuarioResolver(_factory)]),
            new PlantillaRenderer(), TimeProvider.System);
        ProcesarEventoResult registro = await generar.HandleAsync(new ProcesarEventoCommand(Guid.NewGuid(), "PRUEBA", 125,
            new Dictionary<string, string?> { ["Dato"] = "SQL y SMTP" }));

        await using ServidorSmtpPrueba smtp = new();
        SmtpOptions opciones = new()
        {
            Host = "localhost", Puerto = smtp.Puerto, Usuario = "usuario-ficticio", Contrasena = "clave-ficticia",
            ModoTls = ModoTlsSmtp.SslOnConnect, TimeoutTotalSegundos = 5
        };
        SmtpEmailSender sender = new(Options.Create(opciones), NullLogger<SmtpEmailSender>.Instance, () => new SmtpClient
        {
            ServerCertificateValidationCallback = (_, certificado, _, errores) =>
            {
                using X509Chain cadena = new();
                cadena.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                cadena.ChainPolicy.CustomTrustStore.Add(smtp.Certificado);
                cadena.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return (errores & SslPolicyErrors.RemoteCertificateNameMismatch) == 0
                    && certificado is X509Certificate2 cert && cadena.Build(cert);
            }
        });
        ProcesarNotificacionesPendientesHandler procesar = new(_repository, sender,
            Options.Create(new NotificacionProcesamientoOptions()), TimeProvider.System,
            NullLogger<ProcesarNotificacionesPendientesHandler>.Instance);
        await procesar.HandleAsync();
        Notificacion final = (await _repository.ObtenerPorIdAsync(registro.IdNotificacion!.Value))!;
        Assert.That(final.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Enviada));
        Assert.That(final.Intentos, Is.EqualTo(1));
        Assert.That(smtp.Destinatarios.Single(), Does.Contain("aislado@example.com"));
    }

    [Test]
    public async Task Configuracion_AltaActualizacionDesactivacionYCatalogoSinAsumirEnums()
    {
        (int evento, int tipo) = await CrearEvento();
        Assert.That(await _configuracion.ObtenerPorCodigoEventoAsync("INEXISTENTE"), Is.Null);
        ConfiguracionNotificacion sinComponentes = (await _configuracion.ObtenerPorCodigoEventoAsync("PRUEBA"))!;
        Assert.That(sinComponentes.Regla, Is.Null);
        Assert.That(sinComponentes.Plantilla, Is.Null);

        ReglaNotificacion regla = ReglaNotificacion.Crear(evento, "Regla");
        regla.AgregarDestinatario(ReglaDestinatario.Crear(tipo, TipoDestinatario.Rol, "2", NotificacionDestinatario.TipoEnvioConfig.To));
        await _configuracion.GuardarReglaAsync(regla);
        await _configuracion.GuardarPlantillaAsync(PlantillaCorreo.Crear(evento, "Asunto", "<p>Correo</p>"));
        ConfiguracionNotificacion configuracion = (await _configuracion.ObtenerPorCodigoEventoAsync("PRUEBA"))!;
        ReglaDestinatario instruccion = configuracion.Regla!.Destinatarios.Single();
        Assert.That(instruccion.TipoDestinatario, Is.EqualTo(TipoDestinatario.Rol));
        Assert.That(instruccion.IdTipoDestinatario, Is.EqualTo(tipo));
        Assert.That(instruccion.IdTipoDestinatario, Is.Not.EqualTo((int)TipoDestinatario.Rol));

        configuracion.Regla.CambiarNombre("Modificada");
        configuracion.Regla.Desactivar();
        configuracion.Plantilla!.Desactivar();
        await _configuracion.GuardarReglaAsync(configuracion.Regla);
        await _configuracion.GuardarPlantillaAsync(configuracion.Plantilla);
        configuracion = (await _configuracion.ObtenerPorCodigoEventoAsync("PRUEBA"))!;
        Assert.That(configuracion.Regla!.Activo, Is.False);
        Assert.That(configuracion.Plantilla!.Activo, Is.False);
        Assert.That(configuracion.Regla.Destinatarios.Single().IdReglaDestinatario, Is.EqualTo(instruccion.IdReglaDestinatario));
        configuracion.Regla.QuitarDestinatario(configuracion.Regla.Destinatarios.Single());
        await _configuracion.GuardarReglaAsync(configuracion.Regla);
        Assert.That((await _configuracion.ObtenerPorCodigoEventoAsync("PRUEBA"))!.Regla!.Destinatarios, Is.Empty);
    }

    [Test]
    public async Task LecturaConfiguracion_NoMezclaActualizacionConcurrente()
    {
        (int evento, _) = await CrearEvento();
        await _configuracion.GuardarReglaAsync(ReglaNotificacion.Crear(evento, "Antes"));
        await _configuracion.GuardarPlantillaAsync(PlantillaCorreo.Crear(evento, "Antes", "<p>Antes</p>"));
        using IDbConnection db = _factory.CreateConnection();
        using IDbTransaction tx = db.BeginTransaction();
        await db.ExecuteAsync("UPDATE Notificaciones.ReglaNotificacion SET Nombre=N'Después' WHERE IdCodigoEventoNotificacion=@Evento",
            new { Evento = evento }, tx);
        Task<ConfiguracionNotificacion?> lectura = _configuracion.ObtenerPorCodigoEventoAsync("PRUEBA");
        await Task.Delay(100);
        Assert.That(lectura.IsCompleted, Is.False);
        await db.ExecuteAsync("UPDATE Notificaciones.PlantillaCorreo SET Asunto=N'Después' WHERE IdCodigoEventoNotificacion=@Evento",
            new { Evento = evento }, tx);
        tx.Commit();
        ConfiguracionNotificacion leida = (await lectura)!;
        Assert.That(leida.Regla!.Nombre, Is.EqualTo(leida.Plantilla!.Asunto));
    }

    [Test]
    public async Task InstruccionDeOtraRegla_SeRechazaYRevierteCambios()
    {
        (int evento, int tipo) = await CrearEvento();
        ConfiguracionNotificacion inicial = (await _configuracion.ObtenerPorCodigoEventoAsync("REQUERIMIENTO_CREADO"))!;
        ReglaDestinatario ajena = inicial.Regla!.Destinatarios.First();
        ReglaNotificacion invalida = ReglaNotificacion.Reconstruir(0, evento, "Inválida", true, [ajena]);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await _configuracion.GuardarReglaAsync(invalida));
        Assert.That((await _configuracion.ObtenerPorCodigoEventoAsync("PRUEBA"))!.Regla, Is.Null);
    }

    [Test]
    public async Task EstrategiasSql_ResuelvenMultiplesUsuariosYExcluyenInactivosOSinCorreo()
    {
        using IDbConnection db = _factory.CreateConnection();
        int rol = await db.QuerySingleAsync<int>("INSERT INTO seguridad.Rol (Nombre) OUTPUT INSERTED.IdRol VALUES (N'RolPrueba')");
        List<int> usuarios = new();
        foreach ((string? correo, bool activo) in new[] { ("uno@example.com", true), ("dos@example.com", true),
                     ("inactivo@example.com", false), ((string?)null, true), ("  ", true) })
        {
            int usuario = await db.QuerySingleAsync<int>("""
                INSERT INTO seguridad.Usuario (Nombres, Correo, UsuarioLogin, PasswordHash, Activo)
                OUTPUT INSERTED.IdUsuario VALUES (N'Usuario', @Correo, @Login, N'hash-prueba', @Activo);
                """, new { Correo = correo, Activo = activo, Login = Guid.NewGuid().ToString() });
            await db.ExecuteAsync("INSERT INTO seguridad.UsuarioRol (IdUsuario, IdRol) VALUES (@Usuario, @Rol)", new { Usuario = usuario, Rol = rol });
            usuarios.Add(usuario);
        }

        // Una asignación duplicada no duplica las direcciones proyectadas.
        await db.ExecuteAsync("INSERT INTO seguridad.UsuarioRol (IdUsuario, IdRol) VALUES (@Usuario, @Rol)", new { Usuario = usuarios[0], Rol = rol });
        DestinatarioRolResolver porRol = new(_factory);
        DestinatarioUsuarioResolver porUsuario = new(_factory);
        IReadOnlyCollection<DireccionCorreo> direcciones = await porRol.ResolverAsync(ReglaDestinatario.Crear(1,
            TipoDestinatario.Rol, rol.ToString(), NotificacionDestinatario.TipoEnvioConfig.To));
        Assert.That(direcciones.Select(d => d.Correo), Is.EquivalentTo(new[] { "uno@example.com", "dos@example.com" }));

        for (int i = 0; i < usuarios.Count; i++)
        {
            IReadOnlyCollection<DireccionCorreo> usuario = await porUsuario.ResolverAsync(ReglaDestinatario.Crear(2,
                TipoDestinatario.Usuario, usuarios[i].ToString(), NotificacionDestinatario.TipoEnvioConfig.Cc));
            Assert.That(usuario.Count, Is.EqualTo(i < 2 ? 1 : 0));
        }
    }

    private async Task<(int Evento, int Tipo)> CrearEvento()
    {
        using IDbConnection db = _factory.CreateConnection();
        await db.ExecuteAsync("""
            DELETE d FROM Notificaciones.ReglaDestinatario d JOIN Notificaciones.ReglaNotificacion r
                ON r.IdReglaNotificacion=d.IdReglaNotificacion JOIN Notificaciones.CodigoEventoNotificacion e
                ON e.IdCodigoEvento=r.IdCodigoEventoNotificacion WHERE e.Codigo=N'PRUEBA';
            DELETE r FROM Notificaciones.ReglaNotificacion r JOIN Notificaciones.CodigoEventoNotificacion e
                ON e.IdCodigoEvento=r.IdCodigoEventoNotificacion WHERE e.Codigo=N'PRUEBA';
            DELETE p FROM Notificaciones.PlantillaCorreo p JOIN Notificaciones.CodigoEventoNotificacion e
                ON e.IdCodigoEvento=p.IdCodigoEventoNotificacion WHERE e.Codigo=N'PRUEBA';
            DELETE FROM Notificaciones.CodigoEventoNotificacion WHERE Codigo=N'PRUEBA';
            """);
        int evento = await db.QuerySingleAsync<int>("INSERT INTO Notificaciones.CodigoEventoNotificacion (Codigo) OUTPUT INSERTED.IdCodigoEvento VALUES (N'PRUEBA')");
        int tipo = await db.QuerySingleAsync<int>("SELECT IdTipoDestinatario FROM Notificaciones.TipoDestinatario WHERE Nombre=N'ROL'");
        return (evento, tipo);
    }

    private static Notificacion Nueva(Guid? idEvento = null, string asunto = "Asunto original") =>
        Notificacion.Crear(idEvento ?? Guid.NewGuid(), 1, 125, asunto, "<p>Instantánea</p>", AHORA,
            [NotificacionDestinatario.Crear("to@example.com", "Principal", NotificacionDestinatario.TipoEnvioConfig.To),
             NotificacionDestinatario.Crear("cc@example.com", null, NotificacionDestinatario.TipoEnvioConfig.Cc),
             NotificacionDestinatario.Crear("bcc@example.com", null, NotificacionDestinatario.TipoEnvioConfig.Bcc)]);

    private sealed class ConexionPrueba(string connectionString) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection()
        {
            SqlConnection connection = new(connectionString);
            connection.Open();
            return connection;
        }
    }
}
