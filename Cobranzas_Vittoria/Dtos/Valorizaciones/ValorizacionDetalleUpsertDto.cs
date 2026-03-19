namespace Cobranzas_Vittoria.Dtos.Valorizaciones
{
    public class ValorizacionDetalleUpsertDto
    {
        public int? IdDetalle { get; set; }
        public int IdValorizacion { get; set; }
        public DateTime? FechaFactura { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public decimal MontoFactura { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public decimal OtrosDescuentos { get; set; }
        public DateTime? FechaTransferencia { get; set; }
        public string NumeroOperacion { get; set; } = string.Empty;
        public string BancoTransferencia { get; set; } = string.Empty;
        public string BancoDestino { get; set; } = string.Empty;
        public decimal MontoTransferido { get; set; }
        public string Usuario { get; set; } = "system";
    }
}
