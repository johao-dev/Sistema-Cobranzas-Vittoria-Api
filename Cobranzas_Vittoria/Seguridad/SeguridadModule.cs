using System.Text;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cobranzas_Vittoria.Seguridad;

/// <summary>
/// Composicion del modulo: configura autenticacion y autorizacion, une los puertos con sus
/// adaptadores de Infrastructure y registra los casos de uso de Application.
/// </summary>
public static class SeguridadModule
{
    public static IServiceCollection AddSeguridad(this IServiceCollection services, IConfiguration configuration)
    {
        JwtOptions jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwtOptions.Key) ||
            string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
            string.IsNullOrWhiteSpace(jwtOptions.Audience) ||
            jwtOptions.ExpireMinutes <= 0 ||
            jwtOptions.RefreshExpireDays <= 0)
        {
            throw new InvalidOperationException("JWT configuration is incomplete or invalid.");
        }

        services.AddSingleton<IOptions<JwtOptions>>(Options.Create(jwtOptions));
        services.AddHttpContextAccessor();

        // Autenticacion JWT y autorizacion mediante roles y permisos.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        string? token = context.Request.Cookies["access_token"];
                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Puertos de persistencia y servicios auxiliares del modulo.
        services.AddScoped<IPermisoRepository, PermisoRepository>();
        services.AddScoped<IRolRepository, RolRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUsuarioActualService, UsuarioActualService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

        // Cada Handler de Application se registra como scoped.
        IEnumerable<Type> handlers = typeof(SeguridadModule).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.Namespace?.StartsWith("Cobranzas_Vittoria.Seguridad.Application.", StringComparison.Ordinal) == true
                && type.Name.EndsWith("Handler", StringComparison.Ordinal));

        foreach (Type handler in handlers)
        {
            services.AddScoped(handler);
        }

        return services;
    }
}
