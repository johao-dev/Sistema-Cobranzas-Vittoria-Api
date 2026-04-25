using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Dtos.Sunat;
using Cobranzas_Vittoria.Interfaces;
using System.Text.Json;

namespace Cobranzas_Vittoria.Services
{
    public class SunatService : ISunatService
    {
        private readonly HttpClient _httpClient;

        private readonly string _token = "sk_14184.4iWGKjQKNfRrFjXKAcwfhmltXUQRswmB";
        private readonly string _baseUrl = "https://api.decolecta.com/v1";

        public SunatService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ProveedorConsultaSunatDto> ConsultarRucAsync(string ruc)
        {
            var url = $"{_baseUrl}/sunat/ruc?numero={ruc}";
            AgregarHeaders();

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ProveedorConsultaSunatDto>(json);
        }

        public async Task<TipoCambioResponseDto> ConsultarTipoCambio()
        {
            var url = $"{_baseUrl}/tipo-cambio/sbs/average?currency=USD";
            AgregarHeaders();

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TipoCambioResponseDto>(json);
        }

        private void AgregarHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        }
    }
}
