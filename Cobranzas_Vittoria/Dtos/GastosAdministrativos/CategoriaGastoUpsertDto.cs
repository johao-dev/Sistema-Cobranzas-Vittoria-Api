namespace Cobranzas_Vittoria.Dtos.GastosAdministrativos;

public class CategoriaGastoUpsertDto
{
    public int? IdCategoriaGasto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
