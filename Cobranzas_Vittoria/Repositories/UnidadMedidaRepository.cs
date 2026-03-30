using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Interfaces;
using Dapper;
using System.Data;

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
    }
}
