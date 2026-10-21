namespace Cobranzas_Vittoria.Inventario.Domain.Model;

/// <summary>Datos para registrar o actualizar una entrada; la cantidad admite cero.</summary>
public sealed class RegistroKardexEntrada
{
    public int? IdKardexEntrada { get; set; }

    public int IdEspecialidad { get; set; }

    public int IdMaterial { get; set; }

    public int? IdProveedor { get; set; }

    public int? IdProyecto { get; set; }

    public string? NumeroDocumento { get; set; }

    public DateOnly Fecha { get; set; }

    public decimal Cantidad { get; set; }

    public string? Observacion { get; set; }
}
