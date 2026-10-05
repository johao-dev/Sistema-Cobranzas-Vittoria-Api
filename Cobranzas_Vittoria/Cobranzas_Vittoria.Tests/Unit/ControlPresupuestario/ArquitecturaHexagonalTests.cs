using System.Text.RegularExpressions;

namespace Cobranzas_Vittoria.Tests.Unit.ControlPresupuestario;

/// <summary>
/// Reglas de dependencia de los módulos hexagonales (ControlPresupuestario y Gastos directos):
/// el dominio no depende de nada externo y la aplicación no conoce a sus adaptadores.
/// Se verifican sobre las directivas using del código fuente para que no dependan de la revisión humana.
/// </summary>
public class ArquitecturaHexagonalTests
{
    private static readonly (string Carpeta, string Namespace)[] Modulos =
    {
        ("ControlPresupuestario", "Cobranzas_Vittoria.ControlPresupuestario"),
        (Path.Combine("Contable", "GastosDirectos"), "Cobranzas_Vittoria.Contable.GastosDirectos")
    };

    private static IEnumerable<TestCaseData> Casos() => Modulos.Select(m => new TestCaseData(m.Carpeta, m.Namespace)
        .SetArgDisplayNames(m.Namespace));

    private static readonly Regex Using = new(@"^\s*using\s+(?:static\s+)?(?:\w+\s*=\s*)?([\w.]+)\s*;", RegexOptions.Multiline);

    [TestCaseSource(nameof(Casos))]
    public void Domain_SoloDependeDeSiMismoYDelFramework(string carpeta, string modulo)
    {
        var violaciones = Dependencias(carpeta, "Domain")
            .Where(d => d.Namespace.StartsWith("Cobranzas_Vittoria.", StringComparison.Ordinal)
                && !d.Namespace.StartsWith(modulo + ".Domain", StringComparison.Ordinal)
                || EsTecnologia(d.Namespace))
            .ToList();

        Assert.That(violaciones, Is.Empty, Describir(violaciones));
    }

    [TestCaseSource(nameof(Casos))]
    public void Application_NoConoceAdaptadoresNiTecnologia(string carpeta, string modulo)
    {
        var violaciones = Dependencias(carpeta, "Application")
            .Where(d => d.Namespace.StartsWith(modulo + ".Infrastructure", StringComparison.Ordinal)
                || d.Namespace.StartsWith(modulo + ".Presentation", StringComparison.Ordinal)
                || EsTecnologia(d.Namespace))
            .ToList();

        Assert.That(violaciones, Is.Empty, Describir(violaciones));
    }

    [TestCaseSource(nameof(Casos))]
    public void Infrastructure_NoDependeDePresentation(string carpeta, string modulo)
    {
        var violaciones = Dependencias(carpeta, "Infrastructure")
            .Where(d => d.Namespace.StartsWith(modulo + ".Presentation", StringComparison.Ordinal))
            .ToList();

        Assert.That(violaciones, Is.Empty, Describir(violaciones));
    }

    private static bool EsTecnologia(string ns) =>
        ns.StartsWith("Dapper", StringComparison.Ordinal)
        || ns.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal)
        || ns.StartsWith("System.Data", StringComparison.Ordinal)
        || ns.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal);

    private static IEnumerable<(string Archivo, string Namespace)> Dependencias(string modulo, string capa)
    {
        var carpeta = Path.Combine(RaizProyecto(), modulo, capa);
        Assert.That(Directory.Exists(carpeta), Is.True, $"No existe la capa {carpeta}");
        return Directory.EnumerateFiles(carpeta, "*.cs", SearchOption.AllDirectories)
            .SelectMany(archivo => Using.Matches(File.ReadAllText(archivo))
                .Select(m => (Path.GetRelativePath(carpeta, archivo), m.Groups[1].Value)));
    }

    private static string RaizProyecto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cobranzas_Vittoria.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encontró Cobranzas_Vittoria.csproj");
    }

    private static string Describir(IEnumerable<(string Archivo, string Namespace)> violaciones)
        => string.Join(Environment.NewLine, violaciones.Select(v => $"{v.Archivo}: using {v.Namespace}"));
}
