using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Dtos.Sunat;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cobranzas_Vittoria.Services
{
    public class SunatService : ISunatService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SunatService> _logger;
        private readonly string _cacheKey = "TipoCambio_SBS";

        // Decolecta Config
        private readonly string _token = "sk_14184.4iWGKjQKNfRrFjXKAcwfhmltXUQRswmB";
        private readonly string _decolectaApiUrl = "https://api.decolecta.com/v1";

        // PeruAPI Config
        private readonly string _apiKeyTipoCambio = "aa2fb86750af69bbfb4747ee551bd85f";
        private readonly string _peruApiUrl = "https://peruapi.com/api";

        public SunatService(HttpClient httpClient, IMemoryCache cache, ILogger<SunatService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ProveedorConsultaSunatDto> ConsultarRucAsync(string ruc)
        {
            var url = $"{_decolectaApiUrl}/sunat/ruc?numero={ruc}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
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

            // 1. Intentar obtener de Caché
            if (_cache.TryGetValue(cacheKey, out TipoCambioResponseDto cachedRate))
            {
                _logger.LogInformation("Tipo de cambio recuperado de Caché para: {Fecha}", fechaFinal);
                return cachedRate;
            }

            // 2. INTENTO 1: PeruAPI (Principal para históricos)
            _logger.LogInformation("Iniciando consulta de tipo de cambio en PeruAPI para: {Fecha}", fechaFinal);
            var resultado = await IntentarPeruApi(fechaFinal);

            // 3. INTENTO 2: Fallback con Decolecta (Si el primero falló)
            if (resultado == null)
            {
                _logger.LogWarning("PeruAPI falló o no está disponible. Iniciando FALLBACK con Decolecta para: {Fecha}", fechaFinal);
                resultado = await IntentarDecolecta(fechaFinal);
            }

            // 4. Si ambos fallan, lanza excepción para logging
            if (resultado == null)
            {
                _logger.LogCritical("FALLO TOTAL: Ningún proveedor de tipo de cambio pudo procesar la solicitud para: {Fecha}", fechaFinal);
                throw new Exception($"ERROR_PROVEEDORES_TIPO_CAMBIO: No se pudo obtener datos de PeruAPI ni de Decolecta para la fecha {fechaFinal}. Revise los logs para ver detalles de la IP.");
            }

            // 5. Guardar en Caché y retornar
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(6))
                .SetSlidingExpiration(TimeSpan.FromHours(2));

            _cache.Set(cacheKey, resultado, cacheOptions);
            return resultado;
        }

        private async Task<TipoCambioResponseDto?> IntentarPeruApi(string fecha)
        {
            try
            {
                string url = $"{_peruApiUrl}/tipo_cambio?fecha={fecha}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-API-KEY", _apiKeyTipoCambio);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Fallo en PeruAPI. Status: {Status}. Detalle: {Body}", response.StatusCode, errorBody);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<PeruApiResponse>(json);

                if (data == null || data.Code != "200")
                {
                    _logger.LogWarning("PeruAPI respondió pero con código interno inválido: {Json}", json);
                    return null;
                }

                return new TipoCambioResponseDto
                {
                    PrecioCompra = data.Compra,
                    PrecioVenta = data.Venta,
                    MonedaBase = data.Moneda,
                    CotizacionDeDivisa = "PEN",
                    Fecha = fecha
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error de conexión al intentar consultar PeruAPI.");
                return null;
            }
        }

        private async Task<TipoCambioResponseDto?> IntentarDecolecta(string fecha)
        {
            try
            {
                string url = $"{_decolectaApiUrl}/tipo-cambio/sbs/average?currency=USD&date={fecha}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {_token}");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Fallo en Decolecta. Status: {Status}. Detalle: {Body}", response.StatusCode, errorBody);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var resultado = JsonSerializer.Deserialize<TipoCambioResponseDto>(json);

                if (resultado != null)
                {
                    // Fuerza la fecha para mantener consistencia visual
                    resultado.Fecha = fecha;
                }

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error de conexión al intentar consultar Decolecta.");
                return null;
            }
        }

        private string GetFechaFinal(string? fechaSolicitada)
        {
            if (string.IsNullOrEmpty(fechaSolicitada))
            {
                TimeZoneInfo peruZone;
                try
                {
                    // Windows ID
                    peruZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
                }
                catch
                {
                    // Linux ID
                    peruZone = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
                }

                var fechaActualPeru = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, peruZone);
                return fechaActualPeru.ToString("yyyy-MM-dd");
            }
            return fechaSolicitada;
        }

        // Clase interna para mapear la respuesta de PeruAPI
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