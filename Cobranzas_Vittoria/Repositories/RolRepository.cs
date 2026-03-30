using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Seguridad;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories
{
    public class RolRepository : RepositoryBase, IRolRepository
    {
        public RolRepository(IDbConnectionFactory factory) : base(factory) { }

        public async Task<IEnumerable<Rol>> ListAsync(bool? activo = null)
        {
            using var db = Open();
            return await db.QueryAsync<Rol>(
                "seguridad.usp_Rol_List",
                new { Activo = activo },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<int> UpsertAsync(RolUpsertDto dto)
        {
            using var db = Open();
            return await db.ExecuteScalarAsync<int>(
                "seguridad.usp_Rol_Upsert",
                new
                {
                    dto.IdRol,
                    dto.NombreRol,
                    dto.Activo
                },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}
