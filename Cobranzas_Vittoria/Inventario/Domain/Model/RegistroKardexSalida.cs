namespace Cobranzas_Vittoria.Inventario.Domain.Model;

/// <summary>Datos de cabecera y detalles para registrar o actualizar una salida manual.</summary>
public sealed class RegistroKardexSalida
{
    public int? IdKardexSalida { get; set; }

    public int IdEspecialidad { get; set; }

    public int? IdProyecto { get; set; }

    public string? NumeroDocumento { get; set; }

    public DateOnly Fecha { get; set; }

    public string Solicitante { get; set; } = string.Empty;

    public string? Observacion { get; set; }

    public List<ItemKardexSalida> Items { get; set; } = new();
}
