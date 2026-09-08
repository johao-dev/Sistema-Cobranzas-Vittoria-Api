namespace Cobranzas_Vittoria.Seguridad.Application.Common;

public interface IRefreshTokenService
{
    string GenerarToken();
    string CalcularHash(string token);
    DateTime ObtenerExpiracionUtc();
}
