namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Importar;

/// <summary>Devuelve la plantilla contractual original incluida en el despliegue.</summary>
public sealed class ObtenerPlantillaGastosDirectosHandler
{
    public byte[] Handle()
    {
        const string recurso = "Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Plantillas.Plantilla_Carga_Masiva_Gastos_Directos.xlsx";
        using var origen = typeof(ObtenerPlantillaGastosDirectosHandler).Assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException("No se encontró la plantilla de gastos directos.");
        using var destino = new MemoryStream();
        origen.CopyTo(destino);
        return destino.ToArray();
    }
}
