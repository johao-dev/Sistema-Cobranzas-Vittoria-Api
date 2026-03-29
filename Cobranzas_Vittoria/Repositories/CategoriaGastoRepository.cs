using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories;

public class CategoriaGastoRepository : RepositoryBase, ICategoriaGastoRepository
{
    public CategoriaGastoRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<CategoriaGasto>> ListAsync(bool? activo)
    {
        using var db = Open();
        const string sql = @"
SELECT
    IdCategoriaGasto,
    Nombre,
    Activo
FROM maestra.CategoriaGasto
WHERE (@Activo IS NULL OR Activo = @Activo)
ORDER BY Nombre;";
        return await db.QueryAsync<CategoriaGasto>(sql, new { Activo = activo });
    }

    public async Task<int> UpsertAsync(CategoriaGastoUpsertDto dto)
    {
        using var db = Open();
        var nombre = (dto.Nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombre))
            throw new InvalidOperationException("Debes ingresar el nombre de la categoría.");

        const string duplicatedSql = @"
SELECT TOP 1 IdCategoriaGasto
FROM maestra.CategoriaGasto
WHERE Nombre = @Nombre AND (@IdCategoriaGasto IS NULL OR IdCategoriaGasto <> @IdCategoriaGasto);";
        var duplicated = await db.QueryFirstOrDefaultAsync<int?>(duplicatedSql, new { Nombre = nombre, dto.IdCategoriaGasto });
        if (duplicated.HasValue)
            throw new InvalidOperationException("Ya existe una categoría con ese nombre.");

        if (dto.IdCategoriaGasto.HasValue && dto.IdCategoriaGasto.Value > 0)
        {
            const string updateSql = @"
UPDATE maestra.CategoriaGasto
SET Nombre = @Nombre,
    Activo = @Activo
WHERE IdCategoriaGasto = @IdCategoriaGasto;
SELECT @IdCategoriaGasto;";
            return await db.ExecuteScalarAsync<int>(updateSql, new { dto.IdCategoriaGasto, Nombre = nombre, dto.Activo });
        }

        const string insertSql = @"
INSERT INTO maestra.CategoriaGasto (Nombre, Activo, FechaCreacion)
VALUES (@Nombre, @Activo, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await db.ExecuteScalarAsync<int>(insertSql, new { Nombre = nombre, dto.Activo });
    }

    public async Task DeleteAsync(int idCategoriaGasto)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE maestra.CategoriaGasto SET Activo = 0 WHERE IdCategoriaGasto = @IdCategoriaGasto;", new { IdCategoriaGasto = idCategoriaGasto });
    }
}
