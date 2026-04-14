using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Interfaces;
using System.Text.Json;

namespace Cobranzas_Vittoria.Services
{
    public class SunatService : ISunatService
    {
        private readonly HttpClient _httpClient;

        // Este token debe ser almacenado de forma segura, por ejemplo en variables de entorno.
        // Lo dejo aquí directamente, pero en producción no es recomendable.
        private readonly string _token = "sk_14184.4iWGKjQKNfRrFjXKAcwfhmltXUQRswmB";

        public SunatService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ProveedorConsultaSunatDto> ConsultarRucAsync(string ruc)
        {
            var url = $"https://api.decolecta.com/v1/sunat/ruc?numero={ruc}";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ProveedorConsultaSunatDto>(json);
        }
    }
}
