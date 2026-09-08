namespace Cobranzas_Vittoria.Seguridad.Application.Common;

/// <summary>
/// Servicio de aplicacion que resuelve el usuario que ejecuta la operacion.
/// </summary>
public interface IUsuarioActualService
{
    string ObtenerUsuarioActual();
}
