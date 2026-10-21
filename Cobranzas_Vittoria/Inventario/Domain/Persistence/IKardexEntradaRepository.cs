using Cobranzas_Vittoria.Inventario.Domain.Model;

namespace Cobranzas_Vittoria.Inventario.Domain.Persistence;

/// <summary>Contrato de persistencia del Kardex manual con errores de negocio tipados.</summary>
public interface IKardexEntradaRepository
{
    /// <summary>
    /// Lista entradas con los filtros indicados. Ordena por Fecha DESC.
    /// </summary>
    /// <param name="filtro">Filtro de busqueda (todos los campos son opcionales).</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<KardexEntrada>> ListarAsync(
        FiltroKardex filtro,
        CancellationToken ct = default);

    /// <summary>
    /// Inserta una entrada y devuelve la fila creada con sus joins a maestra.
    /// Propaga una excepción de negocio si la operación es rechazada.
    /// </summary>
    /// <param name="dto">Datos de la entrada a crear.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<KardexEntrada> RegistrarAsync(
        RegistroKardexEntrada dto,
        CancellationToken ct = default);

    /// <summary>
    /// Actualiza una entrada existente. Si la dupla (IdMaterial, IdEspecialidad)
    /// cambia, el SP hace rollback del stock en la dupla vieja y aplicacion
    /// en la nueva, todo en la misma TX. Lanza 51104 si el id no existe.
    /// </summary>
    /// <param name="dto">Datos actualizados. <c>IdKardexEntrada</c> es obligatorio.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<KardexEntrada> ActualizarAsync(
        RegistroKardexEntrada dto,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina una entrada. Si la resta dejaria el stock por debajo de las
    /// salidas ya consumidas, el SP lanza 51111 (STOCK_INCONSISTENTE_AL_ELIMINAR).
    /// </summary>
    /// <param name="idKardexEntrada">PK de la entrada a eliminar.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task EliminarAsync(
        int idKardexEntrada,
        CancellationToken ct = default);
}
