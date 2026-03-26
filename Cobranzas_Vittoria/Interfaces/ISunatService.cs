using Cobranzas_Vittoria.Dtos.Maestra;

namespace Cobranzas_Vittoria.Interfaces
{
    public interface ISunatService
    {
        Task<ProveedorConsultaSunatDto> ConsultarRucAsync(string ruc);
    }
}
