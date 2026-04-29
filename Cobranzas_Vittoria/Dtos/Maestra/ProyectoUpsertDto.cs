namespace Cobranzas_Vittoria.Dtos.Maestra
{
    public class ProyectoUpsertDto
    {
        public int? IdProyecto { get; set; }
        public string NombreProyecto { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal CotizacionGeneral { get; set; } = 0;
        public bool Activo { get; set; } = true;
    }
}
