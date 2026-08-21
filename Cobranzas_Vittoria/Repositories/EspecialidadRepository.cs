using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories
{
    public class EspecialidadRepository : RepositoryBase, IEspecialidadRepository
    {
        public EspecialidadRepository(IDbConnectionFactory factory) : base(factory) { }

        public async Task<IEnumerable<Especialidad>> ListAsync(bool? activo)
        {
            using var db = Open();
            return await db.QueryAsync<Especialidad>(
                "maestra.usp_Especialidad_List",
                new { Activo = activo },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<IEnumerable<Especialidad>> ListEnTransaccionAsync(
            bool? activo, IDbConnection cn, IDbTransaction? tx, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(cn);
            // tx puede ser null: este overload acepta lecturas sin transaccion.
            return await cn.QueryAsync<Especialidad>(
                new CommandDefinition(
                    commandText: "maestra.usp_Especialidad_List",
                    parameters: new { Activo = activo },
                    transaction: tx,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));
        }

        public async Task<int> UpsertAsync(EspecialidadUpsertDto dto)
        {
            using var db = Open();
            return await db.ExecuteScalarAsync<int>(
                "maestra.usp_Especialidad_Upsert",
                new { dto.IdEspecialidad, dto.Nombre, dto.Descripcion, dto.Activo },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<int> UpsertEnTransaccionAsync(
            EspecialidadUpsertDto dto,
            IDbConnection cn, IDbTransaction tx, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(cn);
            ArgumentNullException.ThrowIfNull(tx);
            return await cn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    commandText: "maestra.usp_Especialidad_Upsert",
                    parameters: new { dto.IdEspecialidad, dto.Nombre, dto.Descripcion, dto.Activo },
                    transaction: tx,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));
        }
    }
}
