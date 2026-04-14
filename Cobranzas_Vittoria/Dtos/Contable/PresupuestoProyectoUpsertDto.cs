namespace Cobranzas_Vittoria.Dtos.Contable
{
    public class PresupuestoProyectoUpsertDto
    {
        public int IdProyecto { get; set; }
        public List<PresupuestoProyectoItemDto> Items { get; set; } = new();
    }

    public class PresupuestoProyectoItemDto
    {
        public string Concepto { get; set; } = string.Empty;
        public decimal Soles { get; set; }
        public decimal Incidencia { get; set; }
    }
}
