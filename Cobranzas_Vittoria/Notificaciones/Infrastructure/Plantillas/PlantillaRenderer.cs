using System.Net;
using System.Text.RegularExpressions;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using HtmlAgilityPack;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.Plantillas;

/// <summary>Procesa el árbol HTML; solo permite variables en texto y href completo entre comillas.</summary>
public sealed class PlantillaRenderer : IPlantillaRenderer
{
    private static readonly Regex MARCADOR = new(@"\{\{([A-Za-z_][A-Za-z0-9_]*)\}\}",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly HashSet<string> ELEMENTOS = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "head", "meta", "title", "body", "p", "div", "span", "a", "h1", "h2", "h3", "h4",
        "h5", "h6", "strong", "b", "em", "i", "u", "small", "br", "hr", "ul", "ol", "li", "table",
        "thead", "tbody", "tfoot", "tr", "td", "th", "blockquote"
    };
    private static readonly HashSet<string> ATRIBUTOS = new(StringComparer.OrdinalIgnoreCase)
    {
        "lang", "charset", "title", "class", "id", "width", "height", "align", "valign", "border",
        "cellpadding", "cellspacing", "colspan", "rowspan", "href"
    };

    public Task<ContenidoCorreoRenderizado> RenderizarAsync(string codigoEvento, PlantillaCorreo plantilla,
        IReadOnlyDictionary<string, string?> variables, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int marcadoresProcesados = 0;

        if (ContieneMarcador(MARCADOR.Replace(plantilla.CuerpoHtml, "")))
        {
            throw Invalida();
        }

        string Valor(string clave)
        {
            // La sensibilidad a mayúsculas no depende del comparador del diccionario del consumidor.
            string? valor = variables.FirstOrDefault(par => string.Equals(par.Key, clave, StringComparison.Ordinal)).Value;
            return valor ?? throw new VariablePlantillaFaltanteException(codigoEvento, clave);
        }

        string Sustituir(string texto, bool html)
        {
            if (html)
            {
                marcadoresProcesados += MARCADOR.Matches(texto).Count;
            }
            string resto = MARCADOR.Replace(texto, "");
            if (ContieneMarcador(resto))
            {
                throw Invalida();
            }

            return MARCADOR.Replace(texto, coincidencia =>
                html ? WebUtility.HtmlEncode(Valor(coincidencia.Groups[1].Value)) : Valor(coincidencia.Groups[1].Value));
        }

        string asunto = Sustituir(plantilla.Asunto, false);
        if (asunto.Contains('\r') || asunto.Contains('\n'))
        {
            throw new PlantillaCorreoInvalidaException("El asunto no admite saltos de línea.");
        }

        HtmlDocument documento = new();
        documento.LoadHtml(plantilla.CuerpoHtml);

        if (documento.ParseErrors.Any())
        {
            throw Invalida();
        }

        // Materializar antes de modificar el árbol; nunca se interpreta un valor como InnerHtml.
        foreach (HtmlNode nodo in documento.DocumentNode.Descendants().ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (nodo is HtmlTextNode texto)
            {
                texto.Text = Sustituir(texto.Text, true);
                continue;
            }

            if (nodo.NodeType == HtmlNodeType.Comment)
            {
                if (ContieneMarcador(nodo.OuterHtml))
                {
                    throw Invalida();
                }
                continue;
            }

            if (nodo.NodeType != HtmlNodeType.Element || !ELEMENTOS.Contains(nodo.Name))
            {
                throw Invalida();
            }

            HashSet<string> nombres = new(StringComparer.OrdinalIgnoreCase);
            foreach (HtmlAttribute atributo in nodo.Attributes)
            {
                if (!ATRIBUTOS.Contains(atributo.Name) || !nombres.Add(atributo.Name))
                {
                    throw Invalida();
                }

                bool dinamico = ContieneMarcador(atributo.Value);
                if (dinamico && (nodo.Name != "a" || atributo.Name != "href"
                    || atributo.QuoteType == AttributeValueQuote.None || !MARCADOR.IsMatch(atributo.Value)
                    || MARCADOR.Match(atributo.Value).Value != atributo.Value))
                {
                    throw Invalida();
                }

                if (atributo.Name == "href")
                {
                    if (dinamico)
                    {
                        marcadoresProcesados++;
                    }
                    if (nodo.Name != "a")
                    {
                        throw Invalida();
                    }

                    string url = dinamico ? Valor(MARCADOR.Match(atributo.Value).Groups[1].Value)
                        : WebUtility.HtmlDecode(atributo.Value);
                    if (url.Any(char.IsControl) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                        || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                        || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
                    {
                        throw new PlantillaCorreoInvalidaException("El enlace debe ser una URL HTTP o HTTPS absoluta válida.");
                    }

                    atributo.Value = WebUtility.HtmlEncode(url);
                    atributo.QuoteType = AttributeValueQuote.DoubleQuote;
                }
            }
        }

        // Un analizador tolerante puede descartar tokens de HTML inválido; ningún marcador debe perderse.
        if (marcadoresProcesados != MARCADOR.Matches(plantilla.CuerpoHtml).Count)
        {
            throw Invalida();
        }

        return Task.FromResult(new ContenidoCorreoRenderizado(asunto, documento.DocumentNode.OuterHtml));
    }

    private static bool ContieneMarcador(string texto) => texto.Contains("{{", StringComparison.Ordinal)
        || texto.Contains("}}", StringComparison.Ordinal);

    private static PlantillaCorreoInvalidaException Invalida() =>
        new("La plantilla contiene estructura, atributos o marcadores no admitidos.");
}
