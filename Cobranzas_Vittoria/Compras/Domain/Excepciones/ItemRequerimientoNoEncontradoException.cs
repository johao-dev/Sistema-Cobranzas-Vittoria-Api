namespace Cobranzas_Vittoria.Compras.Domain.Excepciones;

/// <summary>El ítem no existe o no pertenece al requerimiento indicado.</summary>
public sealed class ItemRequerimientoNoEncontradoException : RecursoComprasNoEncontradoException
{
    public const string Codigo = "REQUERIMIENTO_ITEM_NO_ENCONTRADO";

    public ItemRequerimientoNoEncontradoException(int idRequerimiento, int idRequerimientoDetalle)
        : base(Codigo, $"El ítem {idRequerimientoDetalle} no existe en el requerimiento {idRequerimiento}.") { }
}
