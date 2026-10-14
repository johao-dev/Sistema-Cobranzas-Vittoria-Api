using Cobranzas_Vittoria.Application.Importacion.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Importar;

/// <summary>Extiende los campos del resultado compartido con el resumen completo del lote.</summary>
public sealed record ResultadoImportacionGastosDirectos(string Modulo, string Formato, int FilasInsertadas,
    int TotalFilas, int FilasValidas, int FilasInvalidas, bool Ok, string Resultado,
    IReadOnlyList<DetalleErrorFila> Errores);
