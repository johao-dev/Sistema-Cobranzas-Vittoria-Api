namespace Cobranzas_Vittoria.Entities
{
    public class CategoriaGasto
    {
        public int IdCategoriaGasto { get; set; }
        public string? Codigo { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }
}
