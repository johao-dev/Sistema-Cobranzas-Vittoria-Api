using Cobranzas_Vittoria.Entities;

namespace Cobranzas_Vittoria.Dtos.Requerimientos
{
    public class RequerimientoGetResponseDto
    {
        public Requerimiento? Requerimiento { get; set; }
        public List<RequerimientoDetalle> Items { get; set; } = new();
        public List<RequerimientoValidacion> Validaciones { get; set; } = new();
    }
}
