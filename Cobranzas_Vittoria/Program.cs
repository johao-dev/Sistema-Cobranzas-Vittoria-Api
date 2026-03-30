using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Middleware;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Services;

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
builder.Services.AddScoped<ISunatService, SunatService>();

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AngularCors");
app.UseStaticFiles();
app.MapControllers();
app.Run();
