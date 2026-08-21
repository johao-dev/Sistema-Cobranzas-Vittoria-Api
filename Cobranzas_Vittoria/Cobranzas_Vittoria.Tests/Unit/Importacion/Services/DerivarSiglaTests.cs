using Cobranzas_Vittoria.Application.Importacion.Services;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Services;

/// <summary>
/// Tests del helper estatico <see cref="ResolvedorEntidadesService.DerivarSigla"/>.
///
/// La sigla se concatena con un correlativo para formar el codigo
/// "UM-&lt;SIGLA&gt;-####" de las Unidades de Medida auto-creadas en
/// importacion. Es importante que sea estable y de exactamente 3 caracteres.
/// </summary>
public class DerivarSiglaTests
{
    [Test]
    public void DerivarSigla_NombreConConsonantesSuficientes_TomaPrimeras3Consonantes()
    {
        // "Kilogramo" -> normalizado "KILOGRAMO" -> consonantes: K, L, G -> "KLG"
        Assert.That(ResolvedorEntidadesService.DerivarSigla("Kilogramo"), Is.EqualTo("KLG"));
    }

    [Test]
    public void DerivarSigla_NombreCorto_RellenaConX()
    {
        // "B" -> normalizado "B" -> consonantes: B (1) -> rellena: "BXX"
        Assert.That(ResolvedorEntidadesService.DerivarSigla("B"), Is.EqualTo("BXX"));
    }

    [Test]
    public void DerivarSigla_NombreSinConsonantes_TodasX()
    {
        // "Aua" -> normalizado "AUA" -> consonantes: ninguna -> "XXX"
        Assert.That(ResolvedorEntidadesService.DerivarSigla("Aua"), Is.EqualTo("XXX"));
    }

    [Test]
    public void DerivarSigla_NombreConAcentos_IgnoraDiacriticos()
    {
        // "Albañil" -> normalizado "ALBANIL" -> consonantes: L, B, N -> "LBN"
        Assert.That(ResolvedorEntidadesService.DerivarSigla("Albañil"), Is.EqualTo("LBN"));
    }

    [Test]
    public void DerivarSigla_CaseInsensitive()
    {
        // "metro" y "METRO" deben dar la misma sigla.
        Assert.That(ResolvedorEntidadesService.DerivarSigla("metro"),
            Is.EqualTo(ResolvedorEntidadesService.DerivarSigla("METRO")));
    }

    [Test]
    public void DerivarSigla_NombreVacio_DevuelveXXX()
    {
        // Comportamiento defensivo: entrada vacia -> "XXX" (no falla).
        Assert.That(ResolvedorEntidadesService.DerivarSigla(""), Is.EqualTo("XXX"));
        Assert.That(ResolvedorEntidadesService.DerivarSigla("   "), Is.EqualTo("XXX"));
        Assert.That(ResolvedorEntidadesService.DerivarSigla(null), Is.EqualTo("XXX"));
    }
}
