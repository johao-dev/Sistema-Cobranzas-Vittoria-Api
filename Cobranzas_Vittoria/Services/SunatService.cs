using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Dtos.Sunat;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cobranzas_Vittoria.Services
{
    public class SunatService : ISunatService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly string _cacheKey = "TipoCambio_SBS";

        // Decolecta API token y URL : Servicio de terceros para consultar RUC de una empresa.
        private readonly string _token = "sk_14184.4iWGKjQKNfRrFjXKAcwfhmltXUQRswmB";
        private readonly string _decolectaApiUrl = "https://api.decolecta.com/v1";

        // PeruAPI Api-Key y URL : Servicio de terceros para consultar el tipo de cambio del día.
        private readonly string _apiKeyTipoCambio = "aa2fb86750af69bbfb4747ee551bd85f";
        private readonly string _peruApiUrl = "https://peruapi.com/api";

        public SunatService(HttpClient httpClient, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
        }

        public async Task<ProveedorConsultaSunatDto> ConsultarRucAsync(string ruc)
        {
            var url = $"{_decolectaApiUrl}/sunat/ruc?numero={ruc}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ProveedorConsultaSunatDto>(json);
        }

        public async Task<TipoCambioResponseDto> ConsultarTipoCambio(string? fechaSolicitada)
        {
            string fechaFinal = GetFechaFinal(fechaSolicitada);
            string cacheKey = $"{_cacheKey}_{fechaFinal}";

            if (_cache.TryGetValue(cacheKey, out TipoCambioResponseDto cachedRate))
            {
                return cachedRate;
            }

            string url = $"{_peruApiUrl}/tipo_cambio?fecha={fechaFinal}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-API-KEY", _apiKeyTipoCambio);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<PeruApiResponse>(json);
            if (data == null || data.Code != "200") return null;

            var resultado = new TipoCambioResponseDto
            {
                PrecioCompra = data.Compra,
                PrecioVenta = data.Venta,
                MonedaBase = data.Moneda,
                CotizacionDeDivisa = "PEN",
                Fecha = fechaFinal
            };

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(6))
                .SetSlidingExpiration(TimeSpan.FromHours(2));

            _cache.Set(cacheKey, resultado, cacheOptions);
            return resultado;
        }

        private string GetFechaFinal(string? fechaSolicitada)
        {
            if (string.IsNullOrEmpty(fechaSolicitada))
            {
                var peruZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
                var fechaActualPeru = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, peruZone);
                return fechaActualPeru.ToString("yyyy-MM-dd");
            }
            return fechaSolicitada;
        }

        // Clase interna para deserializar la respuesta de PeruAPI
        private class PeruApiResponse
        {
            [JsonPropertyName("fecha")] public string Fecha { get; set; } = string.Empty;
            [JsonPropertyName("compra")] public string Compra { get; set; } = string.Empty;
            [JsonPropertyName("venta")] public string Venta { get; set; } = string.Empty;
            [JsonPropertyName("moneda")] public string Moneda { get; set; } = string.Empty;
            [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
        }
    }
}
