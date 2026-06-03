using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories
{
    public class MaterialRepository : RepositoryBase, IMaterialRepository
    {
        public MaterialRepository(IDbConnectionFactory factory) : base(factory) { }

        public async Task<IEnumerable<Material>> ListAsync(bool? activo, int? idEspecialidad)
        {
            using var db = Open();
            return await db.QueryAsync<Material>("maestra.usp_Material_List",
                new { Activo = activo, IdEspecialidad = idEspecialidad },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<Material?> GetAsync(int idMaterial)
        {
            using var db = Open();
            return await db.QueryFirstOrDefaultAsync<Material>("maestra.usp_Material_Get", new { IdMaterial = idMaterial }, commandType: CommandType.StoredProcedure);
        }

        public async Task<string> GetSiguienteCodigoAsync()
        {
            using var db = Open();
            return await GenerarCodigoMaterialAsync(db);
        }

        public async Task<int> UpsertAsync(MaterialUpsertDto dto)
        {
            using var db = Open();

            // Codigo = código interno/correlativo del sistema.
            // CodigoProveedor = código manual proporcionado por el proveedor.
            // En edición conservamos el código existente para no pisarlo con vacío desde el front.
            var codigo = dto.IdMaterial.HasValue && dto.IdMaterial.Value > 0
                ? await ObtenerCodigoActualAsync(db, dto.IdMaterial.Value, dto.Codigo)
                : await GenerarCodigoMaterialAsync(db);

            return await db.ExecuteScalarAsync<int>("maestra.usp_Material_Upsert", new
            {
                dto.IdMaterial,
                dto.IdEspecialidad,
                Codigo = codigo,
                CodigoProveedor = string.IsNullOrWhiteSpace(dto.CodigoProveedor) ? null : dto.CodigoProveedor.Trim(),
                dto.Descripcion,
                dto.UnidadMedida,
                dto.StockMinimo,
                dto.Activo
            }, commandType: CommandType.StoredProcedure);
        }

        private static async Task<string> ObtenerCodigoActualAsync(IDbConnection db, int idMaterial, string? codigoRecibido)
        {
            const string sql = @"
SELECT Codigo
FROM maestra.Material
WHERE IdMaterial = @IdMaterial;";

            var codigoActual = await db.ExecuteScalarAsync<string?>(sql, new { IdMaterial = idMaterial });

            if (!string.IsNullOrWhiteSpace(codigoActual))
                return codigoActual.Trim();

            if (!string.IsNullOrWhiteSpace(codigoRecibido))
                return codigoRecibido.Trim();

            return await GenerarCodigoMaterialAsync(db);
        }

        private static async Task<string> GenerarCodigoMaterialAsync(IDbConnection db)
        {
            const string sql = @"
SELECT ISNULL(MAX(TRY_CONVERT(INT,
    REPLACE(
        REPLACE(
            REPLACE(UPPER(LTRIM(RTRIM(Codigo))), 'MAT-', ''),
        'MAT ', ''),
    'MAT', '')
)), 0) + 1
FROM maestra.Material
WHERE UPPER(LTRIM(RTRIM(Codigo))) LIKE 'MAT%';";

            var siguiente = await db.ExecuteScalarAsync<int>(sql);
            return $"MAT-{siguiente:0000}";
        }
    }
}
