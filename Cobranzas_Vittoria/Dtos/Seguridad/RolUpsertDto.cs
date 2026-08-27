namespace Cobranzas_Vittoria.Dtos.Seguridad
{
    public class RolUpsertDto
    {
        public int? IdRol { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
    }
}
