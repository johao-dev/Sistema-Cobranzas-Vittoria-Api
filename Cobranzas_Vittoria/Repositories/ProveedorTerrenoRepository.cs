using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories;

public class ProveedorTerrenoRepository : RepositoryBase, IProveedorTerrenoRepository
{
    public ProveedorTerrenoRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<ProveedorTerreno>> ListAsync(bool? activo)
    {
        using var db = Open();
        const string sql = @"
SELECT
    IdProveedorTerreno,
    RazonSocial,
    Ruc,
    Contacto,
    Telefono,
    Correo,
    Activo,
    FechaCreacion
FROM maestra.ProveedorTerreno
WHERE (@Activo IS NULL OR Activo = @Activo)
ORDER BY RazonSocial;";
        return await db.QueryAsync<ProveedorTerreno>(sql, new { Activo = activo });
    }

    public async Task<int> UpsertAsync(ProveedorTerrenoUpsertDto dto)
    {
        using var db = Open();
        var razonSocial = (dto.RazonSocial ?? string.Empty).Trim();
        var ruc = string.IsNullOrWhiteSpace(dto.Ruc) ? null : dto.Ruc.Trim();
        var contacto = string.IsNullOrWhiteSpace(dto.Contacto) ? null : dto.Contacto.Trim();
        var telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();
        var correo = string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo.Trim();

        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new InvalidOperationException("Debes ingresar la razón social del proveedor.");

        const string duplicatedSql = @"
SELECT TOP 1 IdProveedorTerreno
FROM maestra.ProveedorTerreno
WHERE (RazonSocial = @RazonSocial OR (@Ruc IS NOT NULL AND NULLIF(LTRIM(RTRIM(Ruc)), '') = @Ruc))
  AND (@IdProveedorTerreno IS NULL OR IdProveedorTerreno <> @IdProveedorTerreno);";
        var duplicated = await db.QueryFirstOrDefaultAsync<int?>(duplicatedSql, new
        {
            RazonSocial = razonSocial,
            Ruc = ruc,
            dto.IdProveedorTerreno
        });
        if (duplicated.HasValue)
            throw new InvalidOperationException("Ya existe un proveedor de terreno con la misma razón social o RUC.");

        if (dto.IdProveedorTerreno.HasValue && dto.IdProveedorTerreno.Value > 0)
        {
            const string updateSql = @"
UPDATE maestra.ProveedorTerreno
SET RazonSocial = @RazonSocial,
    Ruc = @Ruc,
    Contacto = @Contacto,
    Telefono = @Telefono,
    Correo = @Correo,
    Activo = @Activo
WHERE IdProveedorTerreno = @IdProveedorTerreno;
SELECT @IdProveedorTerreno;";
            return await db.ExecuteScalarAsync<int>(updateSql, new
            {
                dto.IdProveedorTerreno,
                RazonSocial = razonSocial,
                Ruc = ruc,
                Contacto = contacto,
                Telefono = telefono,
                Correo = correo,
                dto.Activo
            });
        }

        const string insertSql = @"
INSERT INTO maestra.ProveedorTerreno
(
    RazonSocial,
    Ruc,
    Contacto,
    Telefono,
    Correo,
    Activo,
    FechaCreacion
)
VALUES
(
    @RazonSocial,
    @Ruc,
    @Contacto,
    @Telefono,
    @Correo,
    @Activo,
    GETDATE()
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await db.ExecuteScalarAsync<int>(insertSql, new
        {
            RazonSocial = razonSocial,
            Ruc = ruc,
            Contacto = contacto,
            Telefono = telefono,
            Correo = correo,
            dto.Activo
        });
    }

    public async Task DeleteAsync(int idProveedorTerreno)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE maestra.ProveedorTerreno SET Activo = 0 WHERE IdProveedorTerreno = @IdProveedorTerreno;", new { IdProveedorTerreno = idProveedorTerreno });
    }
}
