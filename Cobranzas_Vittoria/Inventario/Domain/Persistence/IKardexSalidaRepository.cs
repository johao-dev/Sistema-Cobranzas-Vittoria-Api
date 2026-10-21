using Cobranzas_Vittoria.Inventario.Domain.Model;

namespace Cobranzas_Vittoria.Inventario.Domain.Persistence;

/// <summary>Contrato de persistencia del Kardex manual con errores de negocio tipados.</summary>
public interface IKardexSalidaRepository
{
    /// <summary>
    /// Lista salidas con los filtros indicados. Una fila por cada item,
    /// repitiendo los datos de cabecera.
    /// </summary>
    /// <param name="filtro">Filtro de busqueda (todos los campos son opcionales).</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<KardexSalida>> ListarAsync(
        FiltroKardex filtro,
        CancellationToken ct = default);

    /// <summary>
    /// Inserta una salida con sus items. Valida stock para cada item dentro
    /// del SP (lanza 51110 si falta). Devuelve la salida con todos sus items
    /// ya persistidos.
    /// </summary>
    /// <param name="dto">Datos de la salida a crear. <c>Items</c> debe tener al menos un elemento.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<KardexSalida>> RegistrarAsync(
        RegistroKardexSalida dto,
        CancellationToken ct = default);

    /// <summary>
    /// Actualiza una salida existente: reemplaza cabecera + items en TX.
    /// Calcula el diff por dupla (IdMaterial, IdEspecialidad) antes de
    /// aplicarlo al stock global.
    /// </summary>
    /// <param name="dto">Datos actualizados. <c>IdKardexSalida</c> es obligatorio.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<KardexSalida>> ActualizarAsync(
        RegistroKardexSalida dto,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina una salida. CASCADE borra sus detalles. El SP repone el stock
    /// global por (IdMaterial, IdEspecialidad).
    /// </summary>
    /// <param name="idKardexSalida">PK de la salida a eliminar.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task EliminarAsync(
        int idKardexSalida,
        CancellationToken ct = default);
}
