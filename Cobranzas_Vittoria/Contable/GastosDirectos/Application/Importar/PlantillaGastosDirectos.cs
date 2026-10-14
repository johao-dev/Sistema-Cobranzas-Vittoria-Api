namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Importar;

/// <summary>Contrato de la plantilla de hasta 200 gastos, con encabezados en la fila 5.</summary>
public static class PlantillaGastosDirectos
{
    public const string Hoja = "Carga masiva";
    public const int MaximoRegistros = 200;
    public static IReadOnlyList<string> Encabezados { get; } = Array.AsReadOnly(new[]
    {
        "Registro", "Categoría código", "Centro de costo", "Partida", "Fecha", "Concepto", "Proveedor",
        "Descripción", "Monto", "Moneda factura", "Monto factura", "Tipo de cambio", "Fecha tipo cambio",
        "Factura(s)", "Estado de fila", "Observaciones de validación"
    });
    public static IReadOnlySet<int> Auxiliares { get; } = new HashSet<int> { 0, 14, 15 };
    public static IReadOnlySet<string> Categorias { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "OTROS", "MARKETING_VENTAS", "TERRENO", "ANTEPROYECTO", "PROYECTO", "ADMINISTRATIVO", "MUNICIPAL"
    };
}
