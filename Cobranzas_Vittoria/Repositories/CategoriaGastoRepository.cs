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
    Codigo,
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
        var codigo = (dto.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(nombre))
            throw new InvalidOperationException("Debes ingresar el nombre de la categoría.");
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 30 || codigo.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
            throw new InvalidOperationException("El código es requerido y solo admite letras, números y guion bajo (máximo 30 caracteres).");

        const string duplicatedSql = @"
SELECT TOP 1 IdCategoriaGasto
FROM maestra.CategoriaGasto
WHERE Nombre = @Nombre AND (@IdCategoriaGasto IS NULL OR IdCategoriaGasto <> @IdCategoriaGasto);";
        var duplicated = await db.QueryFirstOrDefaultAsync<int?>(duplicatedSql, new { Nombre = nombre, dto.IdCategoriaGasto });
        if (duplicated.HasValue)
            throw new InvalidOperationException("Ya existe una categoría con ese nombre.");

        var duplicatedCode = await db.QueryFirstOrDefaultAsync<int?>("""
            SELECT TOP 1 IdCategoriaGasto FROM maestra.CategoriaGasto
            WHERE Codigo = @Codigo AND (@IdCategoriaGasto IS NULL OR IdCategoriaGasto <> @IdCategoriaGasto);
            """, new { Codigo = codigo, dto.IdCategoriaGasto });
        if (duplicatedCode.HasValue)
            throw new InvalidOperationException("Ya existe una categoría con ese código.");

        if (dto.IdCategoriaGasto.HasValue && dto.IdCategoriaGasto.Value > 0)
        {
            var codigoActual = await db.QueryFirstOrDefaultAsync<string?>(
                "SELECT Codigo FROM maestra.CategoriaGasto WHERE IdCategoriaGasto = @IdCategoriaGasto",
                new { dto.IdCategoriaGasto });
            if (codigoActual is not null && !string.Equals(codigoActual, codigo, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El código estable de una categoría no puede modificarse.");

            const string updateSql = @"
UPDATE maestra.CategoriaGasto
SET Codigo = COALESCE(Codigo, @Codigo),
    Nombre = @Nombre,
    Activo = @Activo
WHERE IdCategoriaGasto = @IdCategoriaGasto;
SELECT @IdCategoriaGasto;";
            return await db.ExecuteScalarAsync<int>(updateSql, new { dto.IdCategoriaGasto, Codigo = codigo, Nombre = nombre, dto.Activo });
        }

        const string insertSql = @"
INSERT INTO maestra.CategoriaGasto (Codigo, Nombre, Activo, FechaCreacion)
VALUES (@Codigo, @Nombre, @Activo, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await db.ExecuteScalarAsync<int>(insertSql, new { Codigo = codigo, Nombre = nombre, dto.Activo });
    }

    public async Task DeleteAsync(int idCategoriaGasto)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE maestra.CategoriaGasto SET Activo = 0 WHERE IdCategoriaGasto = @IdCategoriaGasto;", new { IdCategoriaGasto = idCategoriaGasto });
    }
}
