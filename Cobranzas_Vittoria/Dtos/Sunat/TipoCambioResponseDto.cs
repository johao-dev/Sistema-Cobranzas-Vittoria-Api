using System.Text.Json.Serialization;

namespace Cobranzas_Vittoria.Dtos.Sunat
{
    public class TipoCambioResponseDto
    {
        [JsonPropertyName("buy_price")]
        public string PrecioCompra { get; set; } = string.Empty;

        [JsonPropertyName("sell_price")]
        public string PrecioVenta { get; set; } = string.Empty;

        [JsonPropertyName("base_currency")]
        public string MonedaBase { get; set; } = string.Empty;

        [JsonPropertyName("quote_currency")]
        public string CotizacionDeDivisa { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Fecha { get; set; } = string.Empty;
    }
}
