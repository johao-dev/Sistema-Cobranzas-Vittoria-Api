using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories
{
    public class UnidadMedidaRepository : RepositoryBase, IUnidadMedidaRepository
    {
        public UnidadMedidaRepository(IDbConnectionFactory factory) : base(factory)
        {
        }

        public async Task<IEnumerable<UnidadMedidaDto>> ListAsync(bool? activo)
        {
            using var db = Open();

            return await db.QueryAsync<UnidadMedidaDto>(
                "maestra.usp_UnidadMedida_List",
                new { Activo = activo },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<IEnumerable<UnidadMedidaDto>> ListEnTransaccionAsync(
            bool? activo, IDbConnection cn, IDbTransaction? tx, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(cn);
            // tx puede ser null: este overload acepta lecturas sin transaccion.
            return await cn.QueryAsync<UnidadMedidaDto>(
                new CommandDefinition(
                    commandText: "maestra.usp_UnidadMedida_List",
                    parameters: new { Activo = activo },
                    transaction: tx,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));
        }

        public async Task<int> UpsertAsync(UnidadMedidaUpsertDto dto)
        {
            using var db = Open();

            return await db.ExecuteScalarAsync<int>(
                "maestra.usp_UnidadMedida_Upsert",
                new
                {
                    dto.IdUnidadMedida,
                    dto.Codigo,
                    dto.Nombre,
                    dto.Activo
                },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<int> UpsertEnTransaccionAsync(
            UnidadMedidaUpsertDto dto,
            IDbConnection cn, IDbTransaction tx, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(cn);
            ArgumentNullException.ThrowIfNull(tx);
            return await cn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    commandText: "maestra.usp_UnidadMedida_Upsert",
                    parameters: new
                    {
                        dto.IdUnidadMedida,
                        dto.Codigo,
                        dto.Nombre,
                        dto.Activo
                    },
                    transaction: tx,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));
        }
    }
}
