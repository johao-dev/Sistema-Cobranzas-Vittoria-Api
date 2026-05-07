using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories;

public class GastoProyectoRepository : RepositoryBase, IGastoProyectoRepository
{
    public GastoProyectoRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<GastoProyecto>> ListAsync(string tipoModulo, int? idProyecto, string? concepto, string? estado, bool? activo)
    {
        using var db = Open();
        const string sql = @"
SELECT
    gp.IdGastoProyecto,
    gp.TipoModulo,
    gp.IdProyecto,
    p.NombreProyecto AS Proyecto,
    gp.IdProveedorTerreno,
    pt.RazonSocial AS Proveedor,
    gp.Fecha,
    gp.Concepto,
    gp.Moneda,
    gp.MontoSoles,
    gp.MontoDolares,
    gp.FechaTipoCambio,
    gp.TipoCambio,
    gp.Descripcion,
    gp.Estado,
    gp.Activo,
    gp.FechaCreacion,
    ISNULL(docs.TotalFacturas, 0) AS TotalFacturas
FROM contable.GastoProyecto gp
INNER JOIN maestra.Proyecto p ON p.IdProyecto = gp.IdProyecto
LEFT JOIN maestra.ProveedorTerreno pt ON pt.IdProveedorTerreno = gp.IdProveedorTerreno
OUTER APPLY
(
    SELECT COUNT(1) AS TotalFacturas
    FROM contable.GastoProyectoDocumento gd
    WHERE gd.IdGastoProyecto = gp.IdGastoProyecto
      AND gd.TipoDocumento = 'Factura'
) docs
WHERE gp.TipoModulo = @TipoModulo
  AND (@IdProyecto IS NULL OR gp.IdProyecto = @IdProyecto)
  AND (@Concepto IS NULL OR gp.Concepto = @Concepto)
  AND (@Estado IS NULL OR gp.Estado = @Estado)
  AND (@Activo IS NULL OR gp.Activo = @Activo)
ORDER BY gp.Fecha DESC, gp.IdGastoProyecto DESC;";
        return await db.QueryAsync<GastoProyecto>(sql, new
        {
            TipoModulo = tipoModulo,
            IdProyecto = idProyecto,
            Concepto = string.IsNullOrWhiteSpace(concepto) ? null : concepto.Trim().ToUpperInvariant(),
            Estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim(),
            Activo = activo
        });
    }

    public async Task<(GastoProyecto? gasto, IEnumerable<GastoProyectoDocumento> documentos)> GetAsync(int idGastoProyecto)
    {
        using var db = Open();
        const string sql = @"
SELECT
    gp.IdGastoProyecto,
    gp.TipoModulo,
    gp.IdProyecto,
    p.NombreProyecto AS Proyecto,
    gp.IdProveedorTerreno,
    pt.RazonSocial AS Proveedor,
    gp.Fecha,
    gp.Concepto,
    gp.Moneda,
    gp.MontoSoles,
    gp.MontoDolares,
    gp.FechaTipoCambio,
    gp.TipoCambio,
    gp.Descripcion,
    gp.Estado,
    gp.Activo,
    gp.FechaCreacion,
    CAST(0 AS INT) AS TotalFacturas
FROM contable.GastoProyecto gp
INNER JOIN maestra.Proyecto p ON p.IdProyecto = gp.IdProyecto
LEFT JOIN maestra.ProveedorTerreno pt ON pt.IdProveedorTerreno = gp.IdProveedorTerreno
WHERE gp.IdGastoProyecto = @IdGastoProyecto;";
        var gasto = await db.QueryFirstOrDefaultAsync<GastoProyecto>(sql, new { IdGastoProyecto = idGastoProyecto });
        if (gasto is null)
            return (null, Enumerable.Empty<GastoProyectoDocumento>());

        var documentos = await GetDocumentosAsync(idGastoProyecto);
        return (gasto, documentos);
    }

    public async Task<int> UpsertAsync(string tipoModulo, GastoProyectoUpsertDto dto)
    {
        using var db = Open();
        var concepto = (dto.Concepto ?? string.Empty).Trim().ToUpperInvariant();
        var moneda = string.IsNullOrWhiteSpace(dto.Moneda) ? "PEN" : dto.Moneda.Trim().ToUpperInvariant();
        var descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        var estado = string.Equals(dto.Estado, "Inactivo", StringComparison.OrdinalIgnoreCase) ? "Inactivo" : "Activo";
        var tipoCambio = dto.TipoCambio <= 0 ? 3.41m : decimal.Round(dto.TipoCambio, 4);
        var soles = decimal.Round(dto.MontoSoles, 2);
        var dolares = decimal.Round(dto.MontoDolares, 2);

        if (dto.IdProyecto <= 0)
            throw new InvalidOperationException("Debes seleccionar un proyecto.");
        if (string.IsNullOrWhiteSpace(concepto))
            throw new InvalidOperationException("Debes seleccionar o ingresar un concepto.");
        if (soles <= 0 && dolares <= 0)
            throw new InvalidOperationException("Debes ingresar un monto en soles o dólares.");
        if (soles <= 0 && dolares > 0)
            soles = decimal.Round(dolares * tipoCambio, 2, MidpointRounding.AwayFromZero);

        if (dto.IdGastoProyecto.HasValue && dto.IdGastoProyecto.Value > 0)
        {
            const string updateSql = @"
UPDATE contable.GastoProyecto
SET TipoModulo = @TipoModulo,
    IdProyecto = @IdProyecto,
    IdProveedorTerreno = @IdProveedorTerreno,
    Fecha = @Fecha,
    Concepto = @Concepto,
    Moneda = @Moneda,
    MontoSoles = @MontoSoles,
    MontoDolares = @MontoDolares,
    FechaTipoCambio = @FechaTipoCambio,
    TipoCambio = @TipoCambio,
    Descripcion = @Descripcion,
    Estado = @Estado,
    Activo = @Activo,
    FechaActualizacion = GETDATE()
WHERE IdGastoProyecto = @IdGastoProyecto;
SELECT @IdGastoProyecto;";
            return await db.ExecuteScalarAsync<int>(updateSql, new
            {
                dto.IdGastoProyecto,
                TipoModulo = tipoModulo,
                dto.IdProyecto,
                dto.IdProveedorTerreno,
                Fecha = dto.Fecha.Date,
                Concepto = concepto,
                Moneda = moneda,
                MontoSoles = soles,
                MontoDolares = dolares,
                dto.FechaTipoCambio,
                TipoCambio = tipoCambio,
                Descripcion = descripcion,
                Estado = estado,
                dto.Activo
            });
        }

        const string insertSql = @"
INSERT INTO contable.GastoProyecto
(
    TipoModulo,
    IdProyecto,
    IdProveedorTerreno,
    Fecha,
    Concepto,
    Moneda,
    MontoSoles,
    MontoDolares,
    FechaTipoCambio,
    TipoCambio,
    Descripcion,
    Estado,
    Activo,
    FechaCreacion
)
VALUES
(
    @TipoModulo,
    @IdProyecto,
    @IdProveedorTerreno,
    @Fecha,
    @Concepto,
    @Moneda,
    @MontoSoles,
    @MontoDolares,
    @FechaTipoCambio,
    @TipoCambio,
    @Descripcion,
    @Estado,
    @Activo,
    GETDATE()
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await db.ExecuteScalarAsync<int>(insertSql, new
        {
            TipoModulo = tipoModulo,
            dto.IdProyecto,
            dto.IdProveedorTerreno,
            Fecha = dto.Fecha.Date,
            Concepto = concepto,
            Moneda = moneda,
            MontoSoles = soles,
            MontoDolares = dolares,
            dto.FechaTipoCambio,
            TipoCambio = tipoCambio,
            Descripcion = descripcion,
            Estado = estado,
            dto.Activo
        });
    }

    public async Task DeleteAsync(int idGastoProyecto)
    {
        using var db = Open();
        await db.ExecuteAsync(@"
UPDATE contable.GastoProyecto
SET Estado = 'Inactivo', Activo = 0, FechaActualizacion = GETDATE()
WHERE IdGastoProyecto = @IdGastoProyecto;", new { IdGastoProyecto = idGastoProyecto });
    }

    public async Task<IEnumerable<GastoProyectoDocumento>> GetDocumentosAsync(int idGastoProyecto)
    {
        using var db = Open();
        const string sql = @"
SELECT
    IdGastoProyectoDocumento,
    IdGastoProyecto,
    TipoDocumento,
    NombreArchivo,
    RutaArchivo,
    Extension,
    FechaCreacion
FROM contable.GastoProyectoDocumento
WHERE IdGastoProyecto = @IdGastoProyecto
ORDER BY FechaCreacion DESC, IdGastoProyectoDocumento DESC;";
        return await db.QueryAsync<GastoProyectoDocumento>(sql, new { IdGastoProyecto = idGastoProyecto });
    }

    public async Task SaveDocumentosAsync(int idGastoProyecto, IEnumerable<(string TipoDocumento, string NombreArchivo, string RutaArchivo, string? Extension)> docs)
    {
        using var db = Open();
        const string sql = @"
INSERT INTO contable.GastoProyectoDocumento
(
    IdGastoProyecto,
    TipoDocumento,
    NombreArchivo,
    RutaArchivo,
    Extension,
    FechaCreacion
)
VALUES
(
    @IdGastoProyecto,
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
                IdGastoProyecto = idGastoProyecto,
                TipoDocumento = string.Equals(doc.TipoDocumento, "Factura", StringComparison.OrdinalIgnoreCase) ? "Factura" : "Factura",
                doc.NombreArchivo,
                doc.RutaArchivo,
                doc.Extension
            });
        }
    }
}
