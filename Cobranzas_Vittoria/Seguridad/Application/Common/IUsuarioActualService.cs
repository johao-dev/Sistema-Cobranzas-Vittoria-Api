namespace Cobranzas_Vittoria.Seguridad.Application.Common;

/// <summary>
/// Servicio de aplicacion que resuelve el usuario que ejecuta la operacion.
/// Por ahora devuelve un valor default ("sistema") hasta que se conecte
/// la autenticacion via HttpContext.User.
/// </summary>
public interface IUsuarioActualService
{
    string ObtenerUsuarioActual();
}
