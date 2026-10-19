using System.IdentityModel.Tokens.Jwt;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Services;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;
using Microsoft.Extensions.Options;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Infrastructure;

public class JwtServiceTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void CambiarEstado_NuevoTokenReflejaEstadoSinAlterarTokenEmitido(bool habilitar)
    {
        JwtOptions opciones = new JwtOptions
        {
            Key = "seguridad-unit-tests-secret-key-1234567890",
            Issuer = "vittoria-tests",
            Audience = "vittoria-tests"
        };
        JwtService service = new JwtService(Options.Create(opciones));
        Permiso permiso = Permiso.Reconstruir(1, "permiso.estado", "Cambiar estado", "", !habilitar);
        Rol rol = RolBuilder.Nuevo().ConPermiso(permiso).BuildDomain();
        Usuario usuario = UsuarioBuilder.Nuevo().BuildDomain();
        JwtToken tokenAnterior = service.GenerarToken(usuario, new[] { rol });

        if (habilitar)
            permiso.Activar("admin");
        else
            permiso.Desactivar("admin");

        JwtToken tokenNuevo = service.GenerarToken(usuario, new[] { rol });
        JwtSecurityTokenHandler lector = new JwtSecurityTokenHandler();
        JwtSecurityToken anterior = lector.ReadJwtToken(tokenAnterior.Token);
        JwtSecurityToken nuevo = lector.ReadJwtToken(tokenNuevo.Token);

        Assert.Multiple(() =>
        {
            Assert.That(anterior.Claims.Any(claim => claim.Type == "permission" && claim.Value == permiso.Codigo),
                Is.EqualTo(!habilitar));
            Assert.That(nuevo.Claims.Any(claim => claim.Type == "permission" && claim.Value == permiso.Codigo),
                Is.EqualTo(habilitar));
        });
    }
}
