using System.Text.Json.Serialization;

namespace Cobranzas_Vittoria.Dtos.Maestra
{
    public class ProveedorConsultaSunatDto
    {
        [JsonPropertyName("razon_social")]
        public string RazonSocial { get; set; } = string.Empty;

        [JsonPropertyName("numero_documento")]
        public string NumeroDocumento { get; set; } = string.Empty;

        [JsonPropertyName("estado")]
        public string Estado { get; set; } = string.Empty;

        [JsonPropertyName("condicion")]
        public string Condicion { get; set; } = string.Empty;

        [JsonPropertyName("direccion")]
        public string Direccion { get; set; } = string.Empty;

        [JsonPropertyName("ubigeo")]
        public string Ubigeo { get; set; } = string.Empty;

        [JsonPropertyName("via_tipo")]
        public string ViaTipo { get; set; } = string.Empty;

        [JsonPropertyName("via_nombre")]
        public string ViaNombre { get; set; } = string.Empty;

        [JsonPropertyName("zona_codigo")]
        public string ZonaCodigo { get; set; } = string.Empty;

        [JsonPropertyName("zona_tipo")]
        public string ZonaTipo { get; set; } = string.Empty;

        [JsonPropertyName("numero")]
        public string Numero { get; set; } = string.Empty;

        [JsonPropertyName("interior")]
        public string Interior { get; set; } = string.Empty;

        [JsonPropertyName("lote")]
        public string Lote { get; set; } = string.Empty;

        [JsonPropertyName("dpto")]
        public string Dpto { get; set; } = string.Empty;

        [JsonPropertyName("manzana")]
        public string Manzana { get; set; } = string.Empty;

        [JsonPropertyName("kilometro")]
        public string Kilometro { get; set; } = string.Empty;

        [JsonPropertyName("distrito")]
        public string Distrito { get; set; } = string.Empty;

        [JsonPropertyName("provincia")]
        public string Provincia { get; set; } = string.Empty;

        [JsonPropertyName("departamento")]
        public string Departamento { get; set; } = string.Empty;

        [JsonPropertyName("es_agente_retencion")]
        public bool EsAgenteRetencion { get; set; }

        [JsonPropertyName("es_buen_contribuyente")]
        public bool EsBuenContribuyente { get; set; }

        [JsonPropertyName("locales_anexos")]
        public object[]? LocalesAnexos { get; set; }
    }
}