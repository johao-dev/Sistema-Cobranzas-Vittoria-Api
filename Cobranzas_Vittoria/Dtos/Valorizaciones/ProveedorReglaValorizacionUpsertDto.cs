namespace Cobranzas_Vittoria.Dtos.Valorizaciones
{
    public class ProveedorReglaValorizacionUpsertDto
    {
        public int IdProveedor { get; set; }
        public decimal PorcentajeGarantia { get; set; } = 0.05m;
        public decimal PorcentajeDetraccion { get; set; } = 0.04m;
        public string Usuario { get; set; } = "system";
    }
}
