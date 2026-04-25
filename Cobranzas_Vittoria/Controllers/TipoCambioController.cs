using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers
{
    [ApiController]
    [Route("api/tipo-cambio")]
    public class TipoCambioController : ControllerBase
    {
        private readonly ISunatService _sunatService;

        public TipoCambioController(ISunatService sunatService) => _sunatService = sunatService;

        [HttpGet]
        public async Task<IActionResult> GetTipoCambio()
        {
            var tipoCambio = await _sunatService.ConsultarTipoCambio();
            if (tipoCambio == null) return NotFound();
            return Ok(tipoCambio);
        }
    }
}
