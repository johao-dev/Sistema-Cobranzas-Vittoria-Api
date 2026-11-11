namespace Cobranzas_Vittoria.Notificaciones.Application.Options;

public sealed class NotificacionEnlacesOptions
{
    public string UrlBaseFrontend { get; set; } = string.Empty;

    // Ruta comprobada en src/rutas.ts del frontend; el detalle actual se abre en un modal.
    public string RutaRequerimientos { get; set; } = "requerimientos";

    public string ConstruirUrlRequerimientos()
    {
        if (!Uri.TryCreate(UrlBaseFrontend, UriKind.Absolute, out Uri? baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(baseUri.UserInfo) || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new InvalidOperationException("Configure Notificaciones:Enlaces:UrlBaseFrontend con la URL HTTP(S) del frontend.");
        }

        if (string.IsNullOrWhiteSpace(RutaRequerimientos)
            || RutaRequerimientos.StartsWith('/') || RutaRequerimientos.Contains('\\')
            || !Uri.TryCreate(RutaRequerimientos, UriKind.Relative, out Uri? ruta))
        {
            throw new InvalidOperationException("Configure Notificaciones:Enlaces:RutaRequerimientos con una ruta relativa del frontend.");
        }

        return new Uri(new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/"), ruta).AbsoluteUri;
    }
}
