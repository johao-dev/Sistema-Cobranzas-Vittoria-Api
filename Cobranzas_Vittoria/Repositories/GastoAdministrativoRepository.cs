using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories;

public class GastoAdministrativoRepository : RepositoryBase, IGastoAdministrativoRepository
{
    public GastoAdministrativoRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<GastoAdministrativo>> ListAsync(int? idCategoriaGasto, int? idProveedorGastoAdministrativo, bool? activo)
    {
        using var db = Open();
        var legacyProveedorColumn = await HasLegacyProveedorColumnAsync(db);

        var sql = legacyProveedorColumn
            ? @"
SELECT
    ga.IdGastoAdministrativo,
    ga.IdCategoriaGasto,
    cg.Nombre AS Categoria,
    ga.IdProveedorGastoAdministrativo,
    COALESCE(pga.RazonSocial, p.RazonSocial) AS Proveedor,
    ga.Fecha,
    ga.Monto,
    ga.Descripcion,
    ga.Moneda,
    ga.Activo,
    ga.FechaCreacion,
    ISNULL(docs.TotalFacturas, 0) AS TotalFacturas,
    ISNULL(docs.TotalPagos, 0) AS TotalPagos
FROM contable.GastoAdministrativo ga
INNER JOIN maestra.CategoriaGasto cg ON cg.IdCategoriaGasto = ga.IdCategoriaGasto
LEFT JOIN maestra.ProveedorGastoAdministrativo pga ON pga.IdProveedorGastoAdministrativo = ga.IdProveedorGastoAdministrativo
LEFT JOIN maestra.Proveedor p ON p.IdProveedor = ga.IdProveedor
OUTER APPLY
(
    SELECT
        SUM(CASE WHEN TipoDocumento = 'Factura' THEN 1 ELSE 0 END) AS TotalFacturas,
        SUM(CASE WHEN TipoDocumento = 'Pago' THEN 1 ELSE 0 END) AS TotalPagos
    FROM contable.GastoAdministrativoDocumento gd
    WHERE gd.IdGastoAdministrativo = ga.IdGastoAdministrativo
) docs
WHERE (@IdCategoriaGasto IS NULL OR ga.IdCategoriaGasto = @IdCategoriaGasto)
  AND (@IdProveedorGastoAdministrativo IS NULL OR ga.IdProveedorGastoAdministrativo = @IdProveedorGastoAdministrativo)
  AND (@Activo IS NULL OR ga.Activo = @Activo)
ORDER BY ga.Fecha DESC, ga.IdGastoAdministrativo DESC;"
            : @"
SELECT
    ga.IdGastoAdministrativo,
    ga.IdCategoriaGasto,
    cg.Nombre AS Categoria,
    ga.IdProveedorGastoAdministrativo,
    pga.RazonSocial AS Proveedor,
    ga.Fecha,
    ga.Monto,
    ga.Descripcion,
    ga.Moneda,
    ga.Activo,
    ga.FechaCreacion,
    ISNULL(docs.TotalFacturas, 0) AS TotalFacturas,
    ISNULL(docs.TotalPagos, 0) AS TotalPagos
FROM contable.GastoAdministrativo ga
INNER JOIN maestra.CategoriaGasto cg ON cg.IdCategoriaGasto = ga.IdCategoriaGasto
INNER JOIN maestra.ProveedorGastoAdministrativo pga ON pga.IdProveedorGastoAdministrativo = ga.IdProveedorGastoAdministrativo
OUTER APPLY
(
    SELECT
        SUM(CASE WHEN TipoDocumento = 'Factura' THEN 1 ELSE 0 END) AS TotalFacturas,
        SUM(CASE WHEN TipoDocumento = 'Pago' THEN 1 ELSE 0 END) AS TotalPagos
    FROM contable.GastoAdministrativoDocumento gd
    WHERE gd.IdGastoAdministrativo = ga.IdGastoAdministrativo
) docs
WHERE (@IdCategoriaGasto IS NULL OR ga.IdCategoriaGasto = @IdCategoriaGasto)
  AND (@IdProveedorGastoAdministrativo IS NULL OR ga.IdProveedorGastoAdministrativo = @IdProveedorGastoAdministrativo)
  AND (@Activo IS NULL OR ga.Activo = @Activo)
ORDER BY ga.Fecha DESC, ga.IdGastoAdministrativo DESC;";

        return await db.QueryAsync<GastoAdministrativo>(sql, new { IdCategoriaGasto = idCategoriaGasto, IdProveedorGastoAdministrativo = idProveedorGastoAdministrativo, Activo = activo });
    }

    public async Task<(GastoAdministrativo? gasto, IEnumerable<GastoAdministrativoDocumento> documentos)> GetAsync(int idGastoAdministrativo)
    {
        using var db = Open();
        var legacyProveedorColumn = await HasLegacyProveedorColumnAsync(db);

        var sql = legacyProveedorColumn
            ? @"
SELECT
    ga.IdGastoAdministrativo,
    ga.IdCategoriaGasto,
    cg.Nombre AS Categoria,
    ga.IdProveedorGastoAdministrativo,
    COALESCE(pga.RazonSocial, p.RazonSocial) AS Proveedor,
    ga.Fecha,
    ga.Monto,
    ga.Descripcion,
    ga.Moneda,
    ga.Activo,
    ga.FechaCreacion,
    CAST(0 AS INT) AS TotalFacturas,
    CAST(0 AS INT) AS TotalPagos
FROM contable.GastoAdministrativo ga
INNER JOIN maestra.CategoriaGasto cg ON cg.IdCategoriaGasto = ga.IdCategoriaGasto
LEFT JOIN maestra.ProveedorGastoAdministrativo pga ON pga.IdProveedorGastoAdministrativo = ga.IdProveedorGastoAdministrativo
LEFT JOIN maestra.Proveedor p ON p.IdProveedor = ga.IdProveedor
WHERE ga.IdGastoAdministrativo = @IdGastoAdministrativo;"
            : @"
SELECT
    ga.IdGastoAdministrativo,
    ga.IdCategoriaGasto,
    cg.Nombre AS Categoria,
    ga.IdProveedorGastoAdministrativo,
    pga.RazonSocial AS Proveedor,
    ga.Fecha,
    ga.Monto,
    ga.Descripcion,
    ga.Moneda,
    ga.Activo,
    ga.FechaCreacion,
    CAST(0 AS INT) AS TotalFacturas,
    CAST(0 AS INT) AS TotalPagos
FROM contable.GastoAdministrativo ga
INNER JOIN maestra.CategoriaGasto cg ON cg.IdCategoriaGasto = ga.IdCategoriaGasto
INNER JOIN maestra.ProveedorGastoAdministrativo pga ON pga.IdProveedorGastoAdministrativo = ga.IdProveedorGastoAdministrativo
WHERE ga.IdGastoAdministrativo = @IdGastoAdministrativo;";

        var gasto = await db.QueryFirstOrDefaultAsync<GastoAdministrativo>(sql, new { IdGastoAdministrativo = idGastoAdministrativo });
        if (gasto is null)
            return (null, Enumerable.Empty<GastoAdministrativoDocumento>());

        var documentos = await GetDocumentosAsync(idGastoAdministrativo);
        return (gasto, documentos);
    }

    public async Task<int> UpsertAsync(GastoAdministrativoUpsertDto dto)
    {
        using var db = Open();
        var descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        var moneda = string.IsNullOrWhiteSpace(dto.Moneda) ? "PEN" : dto.Moneda.Trim().ToUpperInvariant();

        if (dto.IdCategoriaGasto <= 0)
            throw new InvalidOperationException("Debes seleccionar una categoría.");
        if (dto.Monto <= 0)
            throw new InvalidOperationException("El monto debe ser mayor a cero.");

        var proveedorGastoId = dto.IdProveedorGastoAdministrativo;
        if (proveedorGastoId <= 0)
            throw new InvalidOperationException("Debes seleccionar un proveedor de gasto.");

        var legacyProveedorColumn = await HasLegacyProveedorColumnAsync(db);
        int? idProveedorCompat = null;

        if (dto.IdGastoAdministrativo.HasValue && dto.IdGastoAdministrativo.Value > 0)
        {
            var updateSql = legacyProveedorColumn
                ? @"
UPDATE contable.GastoAdministrativo
SET IdCategoriaGasto = @IdCategoriaGasto,
    IdProveedorGastoAdministrativo = @IdProveedorGastoAdministrativo,
    IdProveedor = @IdProveedorCompat,
    Fecha = @Fecha,
    Monto = @Monto,
    Descripcion = @Descripcion,
    Moneda = @Moneda,
    Activo = @Activo
WHERE IdGastoAdministrativo = @IdGastoAdministrativo;
SELECT @IdGastoAdministrativo;"
                : @"
UPDATE contable.GastoAdministrativo
SET IdCategoriaGasto = @IdCategoriaGasto,
    IdProveedorGastoAdministrativo = @IdProveedorGastoAdministrativo,
    Fecha = @Fecha,
    Monto = @Monto,
    Descripcion = @Descripcion,
    Moneda = @Moneda,
    Activo = @Activo
WHERE IdGastoAdministrativo = @IdGastoAdministrativo;
SELECT @IdGastoAdministrativo;";

            return await db.ExecuteScalarAsync<int>(updateSql, new
            {
                dto.IdGastoAdministrativo,
                dto.IdCategoriaGasto,
                IdProveedorGastoAdministrativo = proveedorGastoId,
                IdProveedorCompat = idProveedorCompat,
                Fecha = dto.Fecha.Date,
                Monto = decimal.Round(dto.Monto, 2),
                Descripcion = descripcion,
                Moneda = moneda,
                dto.Activo
            });
        }

        var insertSql = legacyProveedorColumn
            ? @"
INSERT INTO contable.GastoAdministrativo
(
    IdCategoriaGasto,
    IdProveedorGastoAdministrativo,
    IdProveedor,
    Fecha,
    Monto,
    Descripcion,
    Moneda,
    Activo,
    FechaCreacion
)
VALUES
(
    @IdCategoriaGasto,
    @IdProveedorGastoAdministrativo,
    @IdProveedorCompat,
    @Fecha,
    @Monto,
    @Descripcion,
    @Moneda,
    @Activo,
    GETDATE()
);
SELECT CAST(SCOPE_IDENTITY() AS INT);"
            : @"
INSERT INTO contable.GastoAdministrativo
(
    IdCategoriaGasto,
    IdProveedorGastoAdministrativo,
    Fecha,
    Monto,
    Descripcion,
    Moneda,
    Activo,
    FechaCreacion
)
VALUES
(
    @IdCategoriaGasto,
    @IdProveedorGastoAdministrativo,
    @Fecha,
    @Monto,
    @Descripcion,
    @Moneda,
    @Activo,
    GETDATE()
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

        return await db.ExecuteScalarAsync<int>(insertSql, new
        {
            dto.IdCategoriaGasto,
            IdProveedorGastoAdministrativo = proveedorGastoId,
            IdProveedorCompat = idProveedorCompat,
            Fecha = dto.Fecha.Date,
            Monto = decimal.Round(dto.Monto, 2),
            Descripcion = descripcion,
            Moneda = moneda,
            dto.Activo
        });
    }

    public async Task DeleteAsync(int idGastoAdministrativo)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE contable.GastoAdministrativo SET Activo = 0 WHERE IdGastoAdministrativo = @IdGastoAdministrativo;", new { IdGastoAdministrativo = idGastoAdministrativo });
    }

    public async Task<IEnumerable<GastoAdministrativoDocumento>> GetDocumentosAsync(int idGastoAdministrativo)
    {
        using var db = Open();
        const string sql = @"
SELECT
    IdGastoAdministrativoDocumento,
    IdGastoAdministrativo,
    TipoDocumento,
    NombreArchivo,
    RutaArchivo,
    Extension,
    FechaCreacion
FROM contable.GastoAdministrativoDocumento
WHERE IdGastoAdministrativo = @IdGastoAdministrativo
ORDER BY FechaCreacion DESC, IdGastoAdministrativoDocumento DESC;";
        return await db.QueryAsync<GastoAdministrativoDocumento>(sql, new { IdGastoAdministrativo = idGastoAdministrativo });
    }

    public async Task SaveDocumentosAsync(int idGastoAdministrativo, IEnumerable<(string TipoDocumento, string NombreArchivo, string RutaArchivo, string? Extension)> docs)
    {
        using var db = Open();
        const string sql = @"
INSERT INTO contable.GastoAdministrativoDocumento
(
    IdGastoAdministrativo,
    TipoDocumento,
    NombreArchivo,
    RutaArchivo,
    Extension,
    FechaCreacion
)
VALUES
(
    @IdGastoAdministrativo,
    @TipoDocumento,
    @NombreArchivo,
    @RutaArchivo,
    @Extension,
    GETDATE()
);";
        foreach (var doc in docs)
        {
            await db.ExecuteAsync(sql, new
            {
                IdGastoAdministrativo = idGastoAdministrativo,
                TipoDocumento = string.Equals(doc.TipoDocumento, "Pago", StringComparison.OrdinalIgnoreCase) ? "Pago" : "Factura",
                doc.NombreArchivo,
                doc.RutaArchivo,
                doc.Extension
            });
        }
    }

    private static async Task<bool> HasLegacyProveedorColumnAsync(IDbConnection db)
    {
        const string sql = @"SELECT CASE WHEN COL_LENGTH('contable.GastoAdministrativo', 'IdProveedor') IS NULL THEN 0 ELSE 1 END;";
        return await db.ExecuteScalarAsync<int>(sql) == 1;
    }

    private static async Task<int?> ResolveOrCreateProveedorGastoIdAsync(IDbConnection db, int idProveedor)
    {
        const string lookupSql = @"
SELECT TOP 1 pga.IdProveedorGastoAdministrativo
FROM maestra.Proveedor p
LEFT JOIN maestra.ProveedorGastoAdministrativo pga
    ON (
        NULLIF(LTRIM(RTRIM(p.Ruc)), '') IS NOT NULL
        AND NULLIF(LTRIM(RTRIM(pga.Ruc)), '') = NULLIF(LTRIM(RTRIM(p.Ruc)), '')
    )
    OR pga.RazonSocial = p.RazonSocial
WHERE p.IdProveedor = @IdProveedor
ORDER BY CASE WHEN NULLIF(LTRIM(RTRIM(pga.Ruc)), '') = NULLIF(LTRIM(RTRIM(p.Ruc)), '') THEN 0 ELSE 1 END, pga.IdProveedorGastoAdministrativo;";
        var existing = await db.QueryFirstOrDefaultAsync<int?>(lookupSql, new { IdProveedor = idProveedor });
        if (existing.HasValue)
            return existing;

        const string sourceSql = @"
SELECT TOP 1
    RazonSocial,
    NULLIF(LTRIM(RTRIM(Ruc)), '') AS Ruc,
    Contacto,
    Telefono,
    Correo,
    Activo
FROM maestra.Proveedor
WHERE IdProveedor = @IdProveedor;";
        var proveedor = await db.QueryFirstOrDefaultAsync<ProveedorCompatDto>(sourceSql, new { IdProveedor = idProveedor });
        if (proveedor is null || string.IsNullOrWhiteSpace(proveedor.RazonSocial))
            return null;

        const string insertSql = @"
INSERT INTO maestra.ProveedorGastoAdministrativo
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

        try
        {
            return await db.ExecuteScalarAsync<int>(insertSql, new
            {
                proveedor.RazonSocial,
                proveedor.Ruc,
                proveedor.Contacto,
                Telefono = proveedor.Telefono,
                Correo = proveedor.Correo,
                Activo = proveedor.Activo
            });
        }
        catch
        {
            return await db.QueryFirstOrDefaultAsync<int?>(lookupSql, new { IdProveedor = idProveedor });
        }
    }

    private static async Task<int?> ResolveOrCreateLegacyProveedorIdAsync(IDbConnection db, int idProveedorGastoAdministrativo)
    {
        const string lookupSql = @"
SELECT TOP 1
    p.IdProveedor
FROM maestra.ProveedorGastoAdministrativo pga
LEFT JOIN maestra.Proveedor p
    ON (
        NULLIF(LTRIM(RTRIM(pga.Ruc)), '') IS NOT NULL
        AND NULLIF(LTRIM(RTRIM(p.Ruc)), '') = NULLIF(LTRIM(RTRIM(pga.Ruc)), '')
    )
    OR p.RazonSocial = pga.RazonSocial
WHERE pga.IdProveedorGastoAdministrativo = @IdProveedorGastoAdministrativo
ORDER BY CASE WHEN NULLIF(LTRIM(RTRIM(p.Ruc)), '') = NULLIF(LTRIM(RTRIM(pga.Ruc)), '') THEN 0 ELSE 1 END, p.IdProveedor;";
        var existing = await db.QueryFirstOrDefaultAsync<int?>(lookupSql, new { IdProveedorGastoAdministrativo = idProveedorGastoAdministrativo });
        if (existing.HasValue)
            return existing;

        const string proveedorAdminSql = @"
SELECT TOP 1
    RazonSocial,
    NULLIF(LTRIM(RTRIM(Ruc)), '') AS Ruc,
    Contacto,
    Telefono,
    Correo,
    Activo
FROM maestra.ProveedorGastoAdministrativo
WHERE IdProveedorGastoAdministrativo = @IdProveedorGastoAdministrativo;";
        var proveedorAdmin = await db.QueryFirstOrDefaultAsync<ProveedorCompatDto>(proveedorAdminSql, new { IdProveedorGastoAdministrativo = idProveedorGastoAdministrativo });
        if (proveedorAdmin is null || string.IsNullOrWhiteSpace(proveedorAdmin.RazonSocial))
            return null;

        const string procExistsSql = @"SELECT CASE WHEN OBJECT_ID('maestra.usp_Proveedor_Upsert', 'P') IS NULL THEN 0 ELSE 1 END;";
        var hasProc = await db.ExecuteScalarAsync<int>(procExistsSql) == 1;
        if (!hasProc)
            return null;

        try
        {
            var newId = await db.ExecuteScalarAsync<int>("maestra.usp_Proveedor_Upsert", new
            {
                IdProveedor = (int?)null,
                RazonSocial = proveedorAdmin.RazonSocial,
                Ruc = proveedorAdmin.Ruc ?? string.Empty,
                Contacto = proveedorAdmin.Contacto,
                Telefono = proveedorAdmin.Telefono,
                Correo = proveedorAdmin.Correo,
                Direccion = (string?)null,
                Banco = (string?)null,
                CuentaCorriente = (string?)null,
                CCI = (string?)null,
                CuentaDetraccion = (string?)null,
                DescripcionServicio = "Proveedor creado automáticamente desde el mantenimiento de gastos administrativos.",
                Observacion = (string?)null,
                TrabajamosConProveedor = "SI",
                Activo = proveedorAdmin.Activo
            }, commandType: CommandType.StoredProcedure);

            return newId > 0 ? newId : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ProveedorCompatDto
    {
        public string RazonSocial { get; set; } = string.Empty;
        public string? Ruc { get; set; }
        public string? Contacto { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public bool Activo { get; set; }
    }
}
