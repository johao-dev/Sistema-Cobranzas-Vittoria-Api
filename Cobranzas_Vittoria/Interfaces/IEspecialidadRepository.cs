using System.Data;
using Cobranzas_Vittoria.Dtos.Maestra;

namespace Cobranzas_Vittoria.Interfaces
{
    /// <summary>
    /// Contrato de acceso a datos para la entidad <c>maestra.Especialidad</c>.
    /// </summary>
    public interface IEspecialidadRepository
    {
        /// <summary>
        /// Lista especialidades filtrando opcionalmente por estado activo.
        /// Usa su propia conexion (no es seguro para compartir transaccion).
        /// </summary>
        Task<IEnumerable<Cobranzas_Vittoria.Entities.Especialidad>> ListAsync(bool? activo);

        /// <summary>
        /// Lista especialidades dentro de la transaccion del caller.
        ///
        /// Se usa en escenarios donde la lectura DEBE ser coherente con una
        /// escritura concurrente del mismo proceso (ej: importacion masiva de
        /// Material, donde la resolucion de catalogos y el INSERT de los
        /// materiales comparten transaccion para atomicidad). Si usaramos
        /// <see cref="ListAsync"/> aqui, abririamos una conexion nueva y
        /// perderiamos la transaccion.
        /// </summary>
        /// <param name="activo">Filtro opcional por estado activo. Null = todos.</param>
        /// <param name="cn">Conexion abierta del caller.</param>
        /// <param name="tx">Transaccion del caller (puede ser null para lecturas sin transaccion).</param>
        /// <param name="ct">Token de cancelacion.</param>
        Task<IEnumerable<Cobranzas_Vittoria.Entities.Especialidad>> ListEnTransaccionAsync(
            bool? activo, IDbConnection cn, IDbTransaction? tx, CancellationToken ct);

        Task<int> UpsertAsync(Cobranzas_Vittoria.Dtos.Maestra.EspecialidadUpsertDto dto);

        /// <summary>
        /// Upsert de Especialidad dentro de la transaccion del caller.
        /// Necesario para la resolucion de catalogos en importacion masiva
        /// (atomicidad: alta de catalogo + INSERT del material comparten
        /// transaccion; si esta falla, ambos hacen rollback).
        /// </summary>
        /// <param name="dto">Datos de la especialidad a crear/actualizar.</param>
        /// <param name="cn">Conexion abierta del caller.</param>
        /// <param name="tx">Transaccion del caller.</param>
        /// <param name="ct">Token de cancelacion.</param>
        /// <returns>IdEspecialidad resultante.</returns>
        Task<int> UpsertEnTransaccionAsync(
            Cobranzas_Vittoria.Dtos.Maestra.EspecialidadUpsertDto dto,
            IDbConnection cn, IDbTransaction tx, CancellationToken ct);
    }
}
