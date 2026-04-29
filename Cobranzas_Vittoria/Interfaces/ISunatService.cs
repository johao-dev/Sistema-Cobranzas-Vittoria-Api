using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Dtos.Sunat;

namespace Cobranzas_Vittoria.Interfaces
{
    public interface ISunatService
    {
        Task<ProveedorConsultaSunatDto> ConsultarRucAsync(string ruc);

        Task<TipoCambioResponseDto> ConsultarTipoCambio(string? fechaSolicitada);
    }
}
