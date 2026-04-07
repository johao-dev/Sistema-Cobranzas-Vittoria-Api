namespace Cobranzas_Vittoria.Dtos.Valorizaciones
{
    public class ProveedorEspecialidadCotizacionUpsertDto
    {
        public int? IdConfiguracion { get; set; }
        public int IdProyecto { get; set; }
        public int IdProveedor { get; set; }
        public int IdEspecialidad { get; set; }
        public string? Empresa { get; set; }
        public string? Servicio { get; set; }
        public string Moneda { get; set; } = "PEN";
        public decimal MontoCotizacion { get; set; }
        public string Usuario { get; set; } = "system";
    }
}
