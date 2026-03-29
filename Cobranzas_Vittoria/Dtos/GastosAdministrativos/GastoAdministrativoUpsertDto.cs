namespace Cobranzas_Vittoria.Dtos.GastosAdministrativos;

public class GastoAdministrativoUpsertDto
{
    public int? IdGastoAdministrativo { get; set; }
    public int IdCategoriaGasto { get; set; }
    public int IdProveedorGastoAdministrativo { get; set; }

    // Compatibilidad con versiones anteriores del módulo que aún envían/leen IdProveedor.
    // Si llega este valor y IdProveedorGastoAdministrativo no viene informado, el repositorio
    // puede usarlo como respaldo para no romper compilación ni requests legacy.
    public int? IdProveedor { get; set; }

    public DateTime Fecha { get; set; }
    public decimal Monto { get; set; }
    public string? Descripcion { get; set; }
    public string Moneda { get; set; } = "PEN";
    public bool Activo { get; set; } = true;
}
