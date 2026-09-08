using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

public class AuthControllerTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/seguridad/auth";

    [Test]
    public async Task Login_CredencialesCorrectas_Retorna200ConTokens()
    {
        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/login", new LoginRequest("admin", "admin123"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(body!.Token.Split('.'), Has.Length.EqualTo(3));
            Assert.That(body.RefreshToken, Is.Not.Empty);
            Assert.That(body.Expiration, Is.GreaterThan(DateTime.UtcNow));
        });
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        Assert.Multiple(() =>
        {
            Assert.That(jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value, Is.EqualTo("1"));
            Assert.That(jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value, Is.EqualTo("admin"));
            Assert.That(jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value),
                Contains.Item("Administrador"));
        });

        var hashPersistido = await DbHelpers.QueryScalarAsync<string>(
            "SELECT TokenHash FROM seguridad.RefreshToken WHERE IdUsuario = @idUsuario",
            new { idUsuario = SeedIds.AdminId });
        Assert.That(hashPersistido, Is.Not.EqualTo(body.RefreshToken));
    }

    [Test]
    public async Task Login_CredencialesIncorrectas_Retorna401()
    {
        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/login", new LoginRequest("admin", "contrasena-invalida"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.That(body.GetProperty("error").GetString(), Is.EqualTo("CREDENCIALES_INVALIDAS"));
    }

    [Test]
    public async Task Refresh_TokenActivo_RotaElRefreshTokenYEmiteNuevoAccessToken()
    {
        LoginResponse login = await LoginAdminAsync();

        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/refresh", new RefreshRequest(login.RefreshToken));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var renovado = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(renovado, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(renovado!.Token, Is.Not.EqualTo(login.Token));
            Assert.That(renovado.RefreshToken, Is.Not.EqualTo(login.RefreshToken));
        });

        var tokenAnterior = await _client.PostAsJsonAsync(
            $"{BaseUrl}/refresh", new RefreshRequest(login.RefreshToken));
        Assert.That(tokenAnterior.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var body = await tokenAnterior.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.That(body.GetProperty("error").GetString(), Is.EqualTo("CREDENCIALES_INVALIDAS"));
    }

    [Test]
    public async Task Logout_RefreshTokenActivo_LoRevoca()
    {
        LoginResponse login = await LoginAdminAsync();

        var logout = await _client.PostAsJsonAsync(
            $"{BaseUrl}/logout", new RefreshRequest(login.RefreshToken));
        var refresh = await _client.PostAsJsonAsync(
            $"{BaseUrl}/refresh", new RefreshRequest(login.RefreshToken));

        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(refresh.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private async Task<LoginResponse> LoginAdminAsync()
    {
        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/login", new LoginRequest("admin", "admin123"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }
}
