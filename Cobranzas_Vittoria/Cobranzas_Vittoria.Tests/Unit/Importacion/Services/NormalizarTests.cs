using Cobranzas_Vittoria.Application.Importacion.Services;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Services;

/// <summary>
/// Tests del helper estatico <see cref="ResolvedorEntidadesService.Normalizar"/>.
///
/// La normalizacion (uppercase + remover acentos via NFD) DEBE coincidir
/// exactamente con la columna computada <c>NombreNormalizado</c> de las tablas
/// <c>maestra.Especialidad</c> y <c>maestra.UnidadMedida</c> en SQL Server
/// (ver V1_2_1__Maestra_Importacion_Tipos_v2.sql). Si no coincide, el lookup
/// por clave normalizada en el resolver falla silenciosamente.
/// </summary>
public class NormalizarTests
{
    [Test]
    public void Normalizar_TextoSinAcentos_DevuelveUppercase()
    {
        Assert.That(ResolvedorEntidadesService.Normalizar("cemento"), Is.EqualTo("CEMENTO"));
    }

    [Test]
    public void Normalizar_TextoConAcentos_EliminaDiacriticos()
    {
        // "Albañilería" -> sin ñ (NFD descompone) -> "ALBANILERIA"
        Assert.That(ResolvedorEntidadesService.Normalizar("Albañilería"), Is.EqualTo("ALBANILERIA"));
    }

    [Test]
    public void Normalizar_TextoConMezclaDeAcentos_EliminaTodosLosDiacriticos()
    {
        // "Eléctricidád ó" -> "ELECTRICIDAD O"
        Assert.That(ResolvedorEntidadesService.Normalizar("Eléctricidád ó"), Is.EqualTo("ELECTRICIDAD O"));
    }

    [Test]
    public void Normalizar_CaseInsensitive_MismaSalidaParaVariantes()
    {
        // El lookup por nombre debe ser case-insensitive: misma clave para mayus/minus/mezcla.
        Assert.That(ResolvedorEntidadesService.Normalizar("CEMENTO"),
            Is.EqualTo(ResolvedorEntidadesService.Normalizar("cemento")));
        Assert.That(ResolvedorEntidadesService.Normalizar("CeMeNtO"),
            Is.EqualTo(ResolvedorEntidadesService.Normalizar("CEMENTO")));
    }

    [Test]
    public void Normalizar_ConEspaciosAlBorde_TrimeaYUppercase()
    {
        // "  Metro  " -> "METRO" (sin espacios al borde, uppercase)
        Assert.That(ResolvedorEntidadesService.Normalizar("  Metro  "), Is.EqualTo("METRO"));
    }

    [Test]
    public void Normalizar_CadenaVaciaODelSoloEspacios_DevuelveVacio()
    {
        Assert.That(ResolvedorEntidadesService.Normalizar(""), Is.EqualTo(""));
        Assert.That(ResolvedorEntidadesService.Normalizar("   "), Is.EqualTo(""));
        Assert.That(ResolvedorEntidadesService.Normalizar(null), Is.EqualTo(""));
    }
}
