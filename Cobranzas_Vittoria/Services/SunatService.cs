using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Dtos.Sunat;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Net.WebSockets;
using System.Text.Json;

namespace Cobranzas_Vittoria.Services
{
    public class SunatService : ISunatService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly string _cacheKey = "TipoCambio_SBS";

        private readonly string _token = "sk_14184.4iWGKjQKNfRrFjXKAcwfhmltXUQRswmB";
        private readonly string _baseUrl = "https://api.decolecta.com/v1";

        public SunatService(HttpClient httpClient, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
        }

        public async Task<ProveedorConsultaSunatDto> ConsultarRucAsync(string ruc)
        {
            var url = $"{_baseUrl}/sunat/ruc?numero={ruc}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ProveedorConsultaSunatDto>(json);
        }

        public async Task<TipoCambioResponseDto> ConsultarTipoCambio(string? fechaSolicitada)
        {
            var peruZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            var fechaActualPeru = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, peruZone);

            string fechaFinal = string.IsNullOrEmpty(fechaSolicitada)
                ? fechaActualPeru.ToString("yyyy-MM-dd")
                : fechaSolicitada;

            string cacheKey = $"{_cacheKey}_{fechaFinal}";

            var url = $"{_baseUrl}/tipo-cambio/sbs/average?currency=USD&date={fechaFinal}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var resultado = JsonSerializer.Deserialize<TipoCambioResponseDto>(json);

            if (resultado != null)
            {
                if (string.IsNullOrEmpty(fechaSolicitada))
                {
                    resultado.Fecha = fechaFinal;
                }

                var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(6))
                .SetSlidingExpiration(TimeSpan.FromHours(2));

                _cache.Set(cacheKey, resultado, cacheOptions);
            }

            return resultado;
        }
    }
}
