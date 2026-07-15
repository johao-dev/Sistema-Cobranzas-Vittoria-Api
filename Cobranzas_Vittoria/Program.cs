using System.Reflection;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Cobranzas_Vittoria.Application.Importacion.Validators;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Infrastructure.Repositories.Importacion;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Middleware;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Services;
using DbUp;
using DbUp.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

builder.Services.AddScoped<UnidadMedidaImportProcessor>();
builder.Services.AddScoped<EspecialidadImportProcessor>();
builder.Services.AddScoped<MaterialImportProcessor>();
builder.Services.AddScoped<ProveedorImportProcessor>();
builder.Services.AddScoped<ProveedorGastoAdministrativoImportProcessor>();
builder.Services.AddScoped<ProveedorTerrenoImportProcessor>();
builder.Services.AddScoped<CategoriaGastoImportProcessor>();

// IImportProcessor se resuelve como la union de todos los processors concretos
// (mecanismo de "tagged convention" via IEnumerable<T> en .NET 8).
builder.Services.AddScoped<IImportService, ImportService>();

var connectionString = builder.Configuration.GetConnectionString("Default");

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
    var resultVersioned =versionedUpgradeEngine.PerformUpgrade();
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

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AngularCors");
app.UseStaticFiles();
app.MapControllers();
app.Run();

public partial class Program { }