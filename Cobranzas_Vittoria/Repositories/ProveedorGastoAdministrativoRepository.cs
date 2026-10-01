using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories;

public class ProveedorGastoAdministrativoRepository : RepositoryBase, IProveedorGastoAdministrativoRepository
{
    public ProveedorGastoAdministrativoRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<ProveedorGastoAdministrativo>> ListAsync(bool? activo, int? idCategoriaGasto)
    {
        using var db = Open();
        const string sql = @"
SELECT
    pga.IdProveedorGastoAdministrativo,
    pga.IdCategoriaGasto,
    cg.Nombre AS Categoria,
    pga.RazonSocial,
    pga.Ruc,
    pga.Contacto,
    pga.Telefono,
    pga.Correo,
    pga.Activo,
    pga.FechaCreacion
FROM maestra.ProveedorGastoAdministrativo pga
LEFT JOIN maestra.CategoriaGasto cg ON cg.IdCategoriaGasto = pga.IdCategoriaGasto
WHERE (@Activo IS NULL OR pga.Activo = @Activo)
  AND (@IdCategoriaGasto IS NULL OR pga.IdCategoriaGasto = @IdCategoriaGasto)
  AND pga.IdCategoriaGasto IS NOT NULL
ORDER BY cg.Nombre, pga.RazonSocial;";
        return await db.QueryAsync<ProveedorGastoAdministrativo>(sql, new { Activo = activo, IdCategoriaGasto = idCategoriaGasto });
    }

    public async Task<int> UpsertAsync(ProveedorGastoAdministrativoUpsertDto dto)
    {
        using var db = Open();
        var razonSocial = (dto.RazonSocial ?? string.Empty).Trim();
        var ruc = string.IsNullOrWhiteSpace(dto.Ruc) ? null : dto.Ruc.Trim();
        var contacto = string.IsNullOrWhiteSpace(dto.Contacto) ? null : dto.Contacto.Trim();
        var telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();
        var correo = string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo.Trim();

        if (dto.IdCategoriaGasto <= 0)
            throw new InvalidOperationException("Debes seleccionar una categoría para el proveedor de gasto.");
        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new InvalidOperationException("Debes ingresar la razón social del proveedor.");

        const string duplicatedSql = @"
SELECT TOP 1 IdProveedorGastoAdministrativo
FROM maestra.ProveedorGastoAdministrativo
WHERE IdCategoriaGasto = @IdCategoriaGasto
  AND (RazonSocial = @RazonSocial OR (@Ruc IS NOT NULL AND NULLIF(LTRIM(RTRIM(Ruc)), '') = @Ruc))
  AND (@IdProveedorGastoAdministrativo IS NULL OR IdProveedorGastoAdministrativo <> @IdProveedorGastoAdministrativo);";
        var duplicated = await db.QueryFirstOrDefaultAsync<int?>(duplicatedSql, new
        {
            dto.IdCategoriaGasto,
            RazonSocial = razonSocial,
            Ruc = ruc,
            dto.IdProveedorGastoAdministrativo
        });
        if (duplicated.HasValue)
            throw new InvalidOperationException("Ya existe un proveedor de gasto para esa categoría con la misma razón social o RUC.");

        if (dto.IdProveedorGastoAdministrativo.HasValue && dto.IdProveedorGastoAdministrativo.Value > 0)
        {
            const string updateSql = @"
UPDATE maestra.ProveedorGastoAdministrativo
SET IdCategoriaGasto = @IdCategoriaGasto,
    RazonSocial = @RazonSocial,
    Ruc = @Ruc,
    Contacto = @Contacto,
    Telefono = @Telefono,
    Correo = @Correo,
    Activo = @Activo
WHERE IdProveedorGastoAdministrativo = @IdProveedorGastoAdministrativo;
SELECT @IdProveedorGastoAdministrativo;";
            return await db.ExecuteScalarAsync<int>(updateSql, new
            {
                dto.IdProveedorGastoAdministrativo,
                dto.IdCategoriaGasto,
                RazonSocial = razonSocial,
                Ruc = ruc,
                Contacto = contacto,
                Telefono = telefono,
                Correo = correo,
                dto.Activo
            });
        }

        const string insertSql = @"
INSERT INTO maestra.ProveedorGastoAdministrativo
(
    IdCategoriaGasto,
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
    @IdCategoriaGasto,
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
            dto.IdCategoriaGasto,
            RazonSocial = razonSocial,
            Ruc = ruc,
            Contacto = contacto,
            Telefono = telefono,
            Correo = correo,
            dto.Activo
        });
    }

    public async Task DeleteAsync(int idProveedorGastoAdministrativo)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE maestra.ProveedorGastoAdministrativo SET Activo = 0 WHERE IdProveedorGastoAdministrativo = @IdProveedorGastoAdministrativo;", new { IdProveedorGastoAdministrativo = idProveedorGastoAdministrativo });
    }
}
