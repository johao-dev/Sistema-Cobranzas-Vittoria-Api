using System.Reflection;
using Cobranzas_Vittoria.Application.Common;
using Cobranzas_Vittoria.Application.Common.Exports;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Cobranzas_Vittoria.Application.Importacion.Validators;
using Cobranzas_Vittoria.Application.Inventario.Persistence;
using Cobranzas_Vittoria.Application.Inventario.Services;
using Cobranzas_Vittoria.Application.Inventario.Validators;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Infrastructure.Repositories.Importacion;
using Cobranzas_Vittoria.Infrastructure.Repositories.Inventario;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Middleware;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Services;
using Cobranzas_Vittoria.Swagger;
using DbUp;
using DbUp.Helpers;
using Dapper;
using Microsoft.Data.SqlClient;
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.IdentityModel.Tokens;
// using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Configuración de autenticación JWT
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options =>
//     {
//         string? jwtKey = builder.Configuration["Jwt:Key"];
//         string? jwtIssuer = builder.Configuration["Jwt:Issuer"];
//         string? jwtAudience = builder.Configuration["Jwt:Audience"];

//         if (string.IsNullOrWhiteSpace(jwtKey))
//             throw new InvalidOperationException("JWT Key is not configured.");
        
//         options.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuer = true,
//             ValidateAudience = true,
//             ValidateLifetime = true,
//             ValidateIssuerSigningKey = true,
//             ValidIssuer = jwtIssuer,
//             ValidAudience = jwtAudience,
//             IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
//         };

//         options.Events = new JwtBearerEvents
//         {
//             OnMessageReceived = context =>
//             {
//                 var token = context.Request.Cookies["access_token"];
//                 if (!string.IsNullOrEmpty(token))
//                 {
//                     context.Token = token;
//                 }
//                 return Task.CompletedTask;
//             }
//         };
//     });

// // Configuración de autorización basada en roles
// builder.Services.AddAuthorization(options =>
// {
//     // Policies base
//     // TODO: Reemplazar por un sistema de códigos de permisos
//     options.AddPolicy("AdminOnly", policy => policy.RequireRole("ADMIN"));
//     options.AddPolicy("PuedeGestionarSeguridad", policy => policy.RequireRole("ADMIN"));
// });

// ============================================================================
// Dapper: registro de TypeHandlers globales
// ============================================================================
// DateOnlyTypeHandler: Dapper en Microsoft.Data.SqlClient 6.x NO soporta
// DateOnly nativamente (ni como parametro de SP ni como propiedad de DTO
// de salida contra columnas DATE). Este handler centraliza la conversion
// DateTime <-> DateOnly para toda la aplicacion. Una sola linea cubre los
// modulos que usen DateOnly en sus DTOs (actualmente: Inventario).
// ============================================================================
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
// ============================================================================
// Swagger / OpenAPI
// ============================================================================
// Solo se registra y se expone en Development y Staging. En Production la
// superficie de descubrimiento de la API no debe estar accesible publicamente.
// ============================================================================
var enableSwagger = builder.Environment.IsDevelopment()
                 || string.Equals(builder.Environment.EnvironmentName, "Staging", StringComparison.OrdinalIgnoreCase);
if (enableSwagger)
{
    builder.Services.AddImportacionSwagger();
}
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularCors", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddHttpClient<ISunatService, SunatService>();
builder.Services.AddMemoryCache(); // soporte para caché
builder.Services.AddTransient<ApiExceptionMiddleware>();

// Repositories
builder.Services.AddScoped<IRolRepository, RolRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IUsuarioRolRepository, UsuarioRolRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IEspecialidadRepository, EspecialidadRepository>();
builder.Services.AddScoped<IProyectoRepository, ProyectoRepository>();
builder.Services.AddScoped<IProveedorRepository, ProveedorRepository>();
builder.Services.AddScoped<IMaterialRepository, MaterialRepository>();
builder.Services.AddScoped<IRequerimientoRepository, RequerimientoRepository>();
builder.Services.AddScoped<IOrdenCompraRepository, OrdenCompraRepository>();
builder.Services.AddScoped<ICompraRepository, CompraRepository>();
builder.Services.AddScoped<IKardexRepository, KardexRepository>();
builder.Services.AddScoped<IUnidadMedidaRepository, UnidadMedidaRepository>();
builder.Services.AddScoped<IValorizacionRepository, ValorizacionRepository>();
builder.Services.AddScoped<ICategoriaGastoRepository, CategoriaGastoRepository>();
builder.Services.AddScoped<IProveedorGastoAdministrativoRepository, ProveedorGastoAdministrativoRepository>();
builder.Services.AddScoped<IGastoAdministrativoRepository, GastoAdministrativoRepository>();
builder.Services.AddScoped<IProveedorTerrenoRepository, ProveedorTerrenoRepository>();
builder.Services.AddScoped<IGastoProyectoRepository, GastoProyectoRepository>();

// Services
builder.Services.AddScoped<IRolService, RolService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IUsuarioRolService, UsuarioRolService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEspecialidadService, EspecialidadService>();
builder.Services.AddScoped<IProyectoService, ProyectoService>();
builder.Services.AddScoped<IProveedorService, ProveedorService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IRequerimientoService, RequerimientoService>();
builder.Services.AddScoped<IOrdenCompraService, OrdenCompraService>();
builder.Services.AddScoped<ICompraService, CompraService>();
builder.Services.AddScoped<IKardexService, KardexService>();
builder.Services.AddScoped<IUnidadMedidaService, UnidadMedidaService>();
builder.Services.AddScoped<IValorizacionService, ValorizacionService>();
builder.Services.AddScoped<ICategoriaGastoService, CategoriaGastoService>();
builder.Services.AddScoped<IProveedorGastoAdministrativoService, ProveedorGastoAdministrativoService>();
builder.Services.AddScoped<IGastoAdministrativoService, GastoAdministrativoService>();
builder.Services.AddScoped<IProveedorTerrenoService, ProveedorTerrenoService>();
builder.Services.AddScoped<IGastoProyectoService, GastoProyectoService>();
builder.Services.AddScoped<ISunatService, SunatService>();

// ============================================================================
// Feature: Importacion masiva
// ============================================================================
// El FileParserResolver y el ImportRepository se registran como scoped porque
// resuelven dependencias por request. El FileValidator y los parsers concretos
// son stateless y se pueden registrar como singleton (se instancian una sola vez).
//
// El ImportService se inyecta con IEnumerable<IImportProcessor> y arma un
// diccionario modulo -> processor en su constructor. Por eso es importante
// registrar TODOS los processors concretos de los 7 modulos soportados
// (UnidadMedida, Especialidad, Material, Proveedor, ProveedorGastoAdministrativo,
// ProveedorTerreno, CategoriaGasto).
// ============================================================================
builder.Services.AddSingleton<IFileParser, CsvFileParser>();
builder.Services.AddSingleton<IFileParser, ExcelFileParser>();
builder.Services.AddSingleton<FileParserResolver>();
builder.Services.AddSingleton<FileValidator>();
builder.Services.AddScoped<IImportRepository, ImportRepository>();

// Cada processor concreto se registra como IImportProcessor (no como su tipo
// concreto) para que IEnumerable<IImportProcessor> se popule con los 7
// implementaciones. Si registramos solo el tipo concreto
// (AddScoped<UnidadMedidaImportProcessor>()), el IEnumerable queda vacio.
builder.Services.AddScoped<IImportProcessor, UnidadMedidaImportProcessor>();
builder.Services.AddScoped<IImportProcessor, EspecialidadImportProcessor>();
builder.Services.AddScoped<IImportProcessor, MaterialImportProcessor>();
builder.Services.AddScoped<IImportProcessor, ProveedorImportProcessor>();
builder.Services.AddScoped<IImportProcessor, ProveedorGastoAdministrativoImportProcessor>();
builder.Services.AddScoped<IImportProcessor, ProveedorTerrenoImportProcessor>();
builder.Services.AddScoped<IImportProcessor, CategoriaGastoImportProcessor>();

// ResolvedorEntidadesService: servicio transversal usado por MaterialImportProcessor
// para resolver IDs de catalogos (Especialidad, UnidadMedida) dentro de la
// transaccion de carga masiva. Scoped porque depende de repos scoped.
builder.Services.AddScoped<ResolvedorEntidadesService>();

// IImportService recibe IEnumerable<IImportProcessor> y arma el diccionario
// modulo -> processor en su constructor.
builder.Services.AddScoped<IImportService, ImportService>();

// ============================================================================
// Feature: Modulo Inventario
// ============================================================================
// Feature aditiva. Los 3 nuevos repositories son la implementacion Dapper
// de los contratos IKardex*Repository definidos en Application/Inventario.
// Todos heredan de RepositoryBase (legacy) y reusan IDbConnectionFactory
// registrada arriba como singleton.
//
// El KardexInventarioValidator consume los repos legacy de Maestra
// (IEspecialidadRepository, IMaterialRepository, IProveedorRepository,
// IProyectoRepository) que ya estan registrados arriba en la seccion
// "Repositories", por lo que NO se vuelven a registrar aca.
//
// KardexInventarioService es scoped porque depende de los 3 repositories
// (scoped) y del validator (scoped). El controller se resuelve por
// ASP.NET Core a partir de IKardexInventarioService.
// ============================================================================
builder.Services.AddScoped<IKardexEntradaRepository, KardexEntradaRepository>();
builder.Services.AddScoped<IKardexSalidaRepository, KardexSalidaRepository>();
builder.Services.AddScoped<IKardexStockRepository, KardexStockRepository>();
builder.Services.AddScoped<KardexInventarioValidator>();
builder.Services.AddScoped<IKardexInventarioService, KardexInventarioService>();

// IExcelExporter es un helper stateless (NPOI construye un workbook nuevo
// por llamada, sin estado de instancia; la cache de reflexion es static).
// Se registra como Singleton por consistencia con otros helpers
// (FileValidator, FileParserResolver) y porque no mantiene conexiones
// ni recursos costosos.
builder.Services.AddSingleton<IExcelExporter, NpoiExcelExporter>();

var connectionString = builder.Configuration.GetConnectionString("Default");

await WaitForSqlServerAsync(connectionString);
await EnsureDatabaseExistsAsync(connectionString);
RunMigrations(connectionString);

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();

// Swagger / OpenAPI: solo Development y Staging (ver bloque equivalente en
// la seccion de servicios). En Production el endpoint /swagger no existe.
if (enableSwagger)
{
    app.UseImportacionSwagger();
}

app.UseCors("AngularCors");
// app.UseAuthentication();
// app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers();
app.Run();

static async Task WaitForSqlServerAsync(string? connectionString)
{
    const int maxAttempts = 12;
    var delay = TimeSpan.FromSeconds(5);
    var serverConnectionString = BuildMasterConnectionString(connectionString);

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await using var connection = new SqlConnection(serverConnectionString);
            await connection.OpenAsync();
            Console.WriteLine("Conexion a SQL Server verificada.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            Console.WriteLine($"SQL Server aun no esta listo (intento {attempt}/{maxAttempts}). Reintentando en {delay.TotalSeconds} segundos. Detalle: {ex.Message}");
            await Task.Delay(delay);
        }
    }

    throw new InvalidOperationException("No se pudo establecer conexion con SQL Server tras varios intentos.");
}

static async Task EnsureDatabaseExistsAsync(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Falta ConnectionStrings:Default");
    }

    var builder = new SqlConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
    {
        throw new InvalidOperationException("La cadena de conexion no define una base de datos.");
    }

    var databaseName = builder.InitialCatalog;
    var serverConnectionString = BuildMasterConnectionString(connectionString);

    await using var connection = new SqlConnection(serverConnectionString);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = """
        IF DB_ID(@databaseName) IS NULL
        BEGIN
            DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName) + N';';
            EXEC(@sql);
        END
        """;
    command.Parameters.AddWithValue("@databaseName", databaseName);

    await command.ExecuteNonQueryAsync();
    Console.WriteLine($"Base de datos lista: {databaseName}.");
}

static void RunMigrations(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Falta ConnectionStrings:Default");
    }

    // Migraciones versionadas: Tablas y estructura
    var versionedUpgradeEngine = DeployChanges.To
        .SqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(
            Assembly.GetExecutingAssembly(),
            filter => filter.Contains(".Migrations.Versioned."))
        .LogToConsole()
        .Build();

    if (versionedUpgradeEngine.IsUpgradeRequired())
    {
        Console.WriteLine("Nuevas migraciones versionadas detectadas. Aplicando...");
        var resultVersioned = versionedUpgradeEngine.PerformUpgrade();
        if (!resultVersioned.Successful)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error en migraciones versionadas: {resultVersioned.Error}");
            Console.ResetColor();
            throw resultVersioned.Error; // Detiene el arranque si falla
        }
    }

    // Migraciones repetibles: Stored Procedures, Views, Typos.
    var repeatableUpgradeEngine = DeployChanges.To
        .SqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(
            Assembly.GetExecutingAssembly(),
            filter => filter.Contains(".Migrations.Repeatable."))
        .JournalTo(new NullJournal()) // NO usa bitácora, se ejecutan SIEMPRE
        .LogToConsole()
        .Build();

    Console.WriteLine("Sincronizando objetos repetibles (CREATE OR ALTER)...");
    var resultRepeatable = repeatableUpgradeEngine.PerformUpgrade();
    if (!resultRepeatable.Successful)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error en objetos repetibles: {resultRepeatable.Error}");
        Console.ResetColor();
        throw resultRepeatable.Error;
    }
}

static string BuildMasterConnectionString(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Falta ConnectionStrings:Default");
    }

    var builder = new SqlConnectionStringBuilder(connectionString)
    {
        InitialCatalog = "master"
    };

    return builder.ConnectionString;
}

public partial class Program { }
