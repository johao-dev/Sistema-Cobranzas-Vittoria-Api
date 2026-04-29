using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories
{
    public class ProyectoRepository : RepositoryBase, IProyectoRepository
    {
        public ProyectoRepository(IDbConnectionFactory factory) : base(factory)
        {
        }

        public async Task<IEnumerable<Proyecto>> ListAsync(bool? activo)
        {
            using var db = Open();

            return await db.QueryAsync<Proyecto>(
                "maestra.usp_Proyecto_List",
                new { Activo = activo },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<int> UpsertAsync(ProyectoUpsertDto dto)
        {
            using var db = Open();

            return await db.ExecuteScalarAsync<int>(
                "maestra.usp_Proyecto_Upsert",
                new
                {
                    dto.IdProyecto,
                    dto.NombreProyecto,
                    dto.Descripcion,
                    dto.CotizacionGeneral,
                    dto.Activo
                },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}
