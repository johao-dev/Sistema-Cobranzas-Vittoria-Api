namespace Cobranzas_Vittoria.Dtos.Seguridad
{
    public class RolUpsertDto
    {
        public int? IdRol { get; set; }
        public string NombreRol { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
    }
}
