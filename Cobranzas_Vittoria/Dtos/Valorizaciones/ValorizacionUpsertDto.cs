namespace Cobranzas_Vittoria.Dtos.Valorizaciones
{
    public class ValorizacionUpsertDto
    {
        public int? IdValorizacion { get; set; }
        public int IdConfiguracion { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public string Observacion { get; set; } = string.Empty;
        public string Usuario { get; set; } = "system";
    }
}
