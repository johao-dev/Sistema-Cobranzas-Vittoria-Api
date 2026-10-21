namespace Cobranzas_Vittoria.Inventario.Domain.Excepciones;

/// <summary>Impide reclasificar materiales con movimientos de inventario.</summary>
public sealed class EspecialidadMaterialConMovimientosException : Exception
{
    public const string CodigoError = "ESPECIALIDAD_MATERIAL_CON_MOVIMIENTOS";

    public EspecialidadMaterialConMovimientosException()
        : base("No se puede cambiar la especialidad de un material con movimientos de inventario.")
    {
    }
}
