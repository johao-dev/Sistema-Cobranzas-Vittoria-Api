using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;

namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Items;

internal static class RequerimientoItemValidator
{
    public static void ValidarIds(int idRequerimiento, int? idDetalle = null)
    {
        if (idRequerimiento <= 0)
            throw ValidacionComprasException.IdentificadorInvalido(nameof(idRequerimiento));

        if (idDetalle is <= 0)
            throw ValidacionComprasException.IdentificadorInvalido(nameof(idDetalle));
    }

    public static void ValidarCampos(CamposItemRequerimiento campos, decimal? cantidad)
    {
        if (campos == CamposItemRequerimiento.Ninguno || (campos & ~CamposItemRequerimiento.Todos) != 0)
            throw new ValidacionComprasException("REQUERIMIENTO_PATCH_INVALIDO",
                "Indica al menos un campo editable: cantidad, partida u observación.");

        if (campos.HasFlag(CamposItemRequerimiento.Cantidad) && cantidad is null)
            throw new ValidacionComprasException("REQUERIMIENTO_CANTIDAD_INVALIDA",
                "La cantidad no puede ser nula.");
    }
}
