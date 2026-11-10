using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Plantillas;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using NUnit.Framework;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Infrastructure;

public class PlantillaRendererTests
{
    private readonly PlantillaRenderer _renderer = new();

    [Test]
    public async Task VariablesGenericas_SeCodificanEnTextoYEnlace()
    {
        PlantillaCorreo plantilla = PlantillaCorreo.Crear(1, "Presupuesto {{Version}}",
            "<p>{{Monto}} {{Monto}}</p><a href=\"{{Enlace}}\">Ver</a>");
        ContenidoCorreoRenderizado resultado = await _renderer.RenderizarAsync("PRESUPUESTO", plantilla,
            new Dictionary<string, string?> { ["Version"] = "V3", ["Monto"] = "<img src=x> & \"dato\"",
                ["Enlace"] = "https://erp.example.com/?a=1&b=\"x\"" });

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Asunto, Is.EqualTo("Presupuesto V3"));
            Assert.That(resultado.CuerpoHtml, Does.Contain("&lt;img src=x&gt; &amp; &quot;dato&quot;"));
            Assert.That(resultado.CuerpoHtml, Does.Contain("href=\"https://erp.example.com/?a=1&amp;b=&quot;x&quot;\""));
            Assert.That(resultado.CuerpoHtml, Does.Not.Contain("<img"));
        });
    }

    [TestCase("https://erp.example.com/r/1")]
    [TestCase("http://localhost/r/1")]
    public void UrlHttpHttps_SeAdmite(string url) => Assert.DoesNotThrowAsync(async () =>
        await Renderizar("<a href=\"{{Dato}}\">Ver</a>", url));

    [TestCase("javascript:alert(1)")]
    [TestCase("data:text/html,test")]
    [TestCase("//erp.example.com/r")]
    [TestCase("/r/1")]
    [TestCase("https://user:pass@erp.example.com/")]
    [TestCase("https://erp.example.com/\r\nheader")]
    public void UrlNoAdmitida_SeRechaza(string url) => Assert.ThrowsAsync<PlantillaCorreoInvalidaException>(async () =>
        await Renderizar("<a href=\"{{Dato}}\">Ver</a>", url));

    [TestCase("<a href={{Dato}}>Ver</a>")]
    [TestCase("<a href=\"https://erp.example.com/{{Dato}}\">Ver</a>")]
    [TestCase("<p title=\"{{Dato}}\">Texto</p>")]
    [TestCase("<p onclick=\"{{Dato}}\">Texto</p>")]
    [TestCase("<p style=\"color:{{Dato}}\">Texto</p>")]
    [TestCase("<script>{{Dato}}</script>")]
    [TestCase("<style>{{Dato}}</style>")]
    [TestCase("<!-- {{Dato}} -->")]
    [TestCase("<{{Dato}}>Texto</{{Dato}}>")]
    [TestCase("<p {{Dato}}=\"dato\">Texto</p>")]
    [TestCase("<a href=\"https://example.com\" href=\"{{Dato}}\">Ver</a>")]
    [TestCase("<p>Texto</p {{Dato}}>")]
    [TestCase("<p>{{Dato</p>")]
    [TestCase("<iframe src=\"https://example.com\"></iframe>")]
    [TestCase("<a href=\"javascript:alert(1)\">Ver</a>")]
    public void ContextoNoAdmitido_SeRechaza(string html) => Assert.ThrowsAsync<PlantillaCorreoInvalidaException>(async () =>
        await Renderizar(html, "https://example.com"));

    [TestCase(false)]
    [TestCase(true)]
    public void VariableAusenteONula_UtilizaExcepcionDelDominio(bool nula)
    {
        Dictionary<string, string?> variables = new();
        if (nula) variables["Dato"] = null;

        Assert.ThrowsAsync<VariablePlantillaFaltanteException>(async () => await _renderer.RenderizarAsync("EVENTO",
            PlantillaCorreo.Crear(1, "Asunto", "<p>{{Dato}}</p>"), variables));
    }

    [Test]
    public void Mayusculas_SeRespetanInclusoConDiccionarioInsensible()
    {
        Dictionary<string, string?> variables = new(StringComparer.OrdinalIgnoreCase) { ["dato"] = "valor" };
        Assert.ThrowsAsync<VariablePlantillaFaltanteException>(async () => await _renderer.RenderizarAsync("EVENTO",
            PlantillaCorreo.Crear(1, "{{Dato}}", "<p>Correo</p>"), variables));
    }

    [TestCase("dato\r\nBcc: oculto")]
    [TestCase("dato\nBcc: oculto")]
    public void AsuntoConSaltos_SeRechaza(string valor) => Assert.ThrowsAsync<PlantillaCorreoInvalidaException>(async () =>
        await _renderer.RenderizarAsync("EVENTO", PlantillaCorreo.Crear(1, "{{Dato}}", "<p>Correo</p>"),
            new Dictionary<string, string?> { ["Dato"] = valor }));

    [Test]
    public void Cancelacion_SeRespeta() => Assert.ThrowsAsync<OperationCanceledException>(async () =>
        await _renderer.RenderizarAsync("EVENTO", PlantillaCorreo.Crear(1, "Asunto", "<p>Correo</p>"),
            new Dictionary<string, string?>(), new CancellationToken(true)));

    private Task<ContenidoCorreoRenderizado> Renderizar(string html, string valor) =>
        _renderer.RenderizarAsync("OTRO_EVENTO", PlantillaCorreo.Crear(1, "Asunto", html),
            new Dictionary<string, string?> { ["Dato"] = valor });
}
