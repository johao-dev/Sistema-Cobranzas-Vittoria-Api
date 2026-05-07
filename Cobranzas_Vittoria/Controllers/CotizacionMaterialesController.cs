using System.Data;
using System.Globalization;
using System.Text;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Contable;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

[ApiController]
[Route("api/contable/cotizacion-materiales")]
public class CotizacionMaterialesController : ControllerBase
{
    private readonly IDbConnectionFactory _factory;
    public CotizacionMaterialesController(IDbConnectionFactory factory) => _factory = factory;

    [HttpGet("{idProyecto:int}")]
    public async Task<IActionResult> GetByProyecto(int idProyecto)
    {
        using var db = _factory.CreateConnection();
        await EnsureTablesAsync(db);

        var items = (await db.QueryAsync(@"
SELECT
    c.IdCotizacionMaterialEspecialidad,
    c.IdProyecto,
    c.IdEspecialidad,
    e.Nombre AS Especialidad,
    c.Cotizacion,
    c.Activo,
    c.FechaCreacion,
    c.FechaActualizacion
FROM contable.CotizacionMaterialEspecialidad c
INNER JOIN maestra.Especialidad e ON e.IdEspecialidad = c.IdEspecialidad
WHERE c.IdProyecto = @IdProyecto AND ISNULL(c.Activo, 1) = 1
ORDER BY e.Nombre;", new { IdProyecto = idProyecto })).ToList();

        return Ok(new
        {
            idProyecto,
            totalCotizacionMateriales = items.Sum(x => (decimal?)x.Cotizacion ?? 0m),
            items = items.Select(x => new
            {
                idCotizacionMaterialEspecialidad = (int?)x.IdCotizacionMaterialEspecialidad,
                idProyecto = (int)x.IdProyecto,
                idEspecialidad = (int)x.IdEspecialidad,
                especialidad = (string)x.Especialidad,
                cotizacion = (decimal)x.Cotizacion
            })
        });
    }

    [HttpGet("resumen/{idProyecto:int}")]
    public async Task<IActionResult> GetResumenByProyecto(int idProyecto)
    {
        using var db = _factory.CreateConnection();
        await EnsureTablesAsync(db);

        var cotizaciones = (await db.QueryAsync<CotizacionMaterialRow>(@"
SELECT
    c.IdEspecialidad,
    e.Nombre AS Especialidad,
    SUM(ISNULL(c.Cotizacion, 0)) AS Cotizacion
FROM contable.CotizacionMaterialEspecialidad c
INNER JOIN maestra.Especialidad e ON e.IdEspecialidad = c.IdEspecialidad
WHERE c.IdProyecto = @IdProyecto
  AND ISNULL(c.Activo, 1) = 1
GROUP BY c.IdEspecialidad, e.Nombre
ORDER BY e.Nombre;", new { IdProyecto = idProyecto })).ToList();

        var facturados = (await db.QueryAsync<FacturadoMaterialRow>(@"
SELECT
    m.IdEspecialidad,
    e.Nombre AS Especialidad,
    SUM(
        ISNULL(
            TRY_CONVERT(DECIMAL(18,2), d.Subtotal),
            ISNULL(TRY_CONVERT(DECIMAL(18,2), d.Cantidad), 0) * ISNULL(TRY_CONVERT(DECIMAL(18,2), d.PrecioUnitario), 0)
        )
    ) AS Facturado
FROM compras.Compra c
INNER JOIN compras.CompraDetalle d ON d.IdCompra = c.IdCompra
INNER JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
INNER JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
WHERE COALESCE(oc.IdProyecto, r.IdProyecto) = @IdProyecto
GROUP BY m.IdEspecialidad, e.Nombre
ORDER BY e.Nombre;", new { IdProyecto = idProyecto })).ToList();

        var usedCotizaciones = new HashSet<int>();
        var result = new List<CotizacionMaterialesResumenItem>();

        foreach (var facturado in facturados)
        {
            var matches = FindCotizacionesForEspecialidad(facturado, cotizaciones)
                .Where(x => !usedCotizaciones.Contains(x.IdEspecialidad))
                .ToList();

            foreach (var match in matches)
                usedCotizaciones.Add(match.IdEspecialidad);

            var cotizacion = matches.Sum(x => x.Cotizacion);
            result.Add(new CotizacionMaterialesResumenItem
            {
                IdEspecialidad = facturado.IdEspecialidad,
                Especialidad = facturado.Especialidad,
                Cotizacion = cotizacion,
                Facturado = facturado.Facturado,
                Saldo = cotizacion - facturado.Facturado
            });
        }

        foreach (var cotizacion in cotizaciones.Where(x => !usedCotizaciones.Contains(x.IdEspecialidad)))
        {
            result.Add(new CotizacionMaterialesResumenItem
            {
                IdEspecialidad = cotizacion.IdEspecialidad,
                Especialidad = cotizacion.Especialidad,
                Cotizacion = cotizacion.Cotizacion,
                Facturado = 0m,
                Saldo = cotizacion.Cotizacion
            });
        }

        result = result
            .OrderBy(x => x.Especialidad)
            .ToList();

        var totalCotizacion = cotizaciones.Sum(x => x.Cotizacion);
        var totalFacturado = facturados.Sum(x => x.Facturado);

        return Ok(new
        {
            idProyecto,
            totalCotizacionMateriales = totalCotizacion,
            totalFacturado,
            totalSaldo = totalCotizacion - totalFacturado,
            items = result.Select(x => new
            {
                idEspecialidad = x.IdEspecialidad,
                especialidad = x.Especialidad,
                cotizacion = x.Cotizacion,
                facturado = x.Facturado,
                saldo = x.Saldo
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] CotizacionMaterialesUpsertDto dto)
    {
        if (dto.IdProyecto <= 0)
            return BadRequest(new { message = "Debes seleccionar un proyecto válido." });

        using var db = _factory.CreateConnection();
        await EnsureTablesAsync(db);
        using var tx = db.BeginTransaction();

        var normalized = (dto.Items ?? new List<CotizacionMaterialEspecialidadItemDto>())
            .Where(x => x.IdEspecialidad > 0)
            .GroupBy(x => x.IdEspecialidad)
            .Select(g => new { IdEspecialidad = g.Key, Cotizacion = decimal.Round(g.Sum(x => x.Cotizacion), 2) })
            .ToList();

        await db.ExecuteAsync(@"
UPDATE contable.CotizacionMaterialEspecialidad
SET Activo = 0, FechaActualizacion = GETDATE()
WHERE IdProyecto = @IdProyecto;", new { dto.IdProyecto }, tx);

        const string upsertSql = @"
IF EXISTS (SELECT 1 FROM contable.CotizacionMaterialEspecialidad WHERE IdProyecto = @IdProyecto AND IdEspecialidad = @IdEspecialidad)
BEGIN
    UPDATE contable.CotizacionMaterialEspecialidad
    SET Cotizacion = @Cotizacion,
        Activo = 1,
        FechaActualizacion = GETDATE()
    WHERE IdProyecto = @IdProyecto AND IdEspecialidad = @IdEspecialidad;
END
ELSE
BEGIN
    INSERT INTO contable.CotizacionMaterialEspecialidad
    (
        IdProyecto,
        IdEspecialidad,
        Cotizacion,
        Activo,
        FechaCreacion
    )
    VALUES
    (
        @IdProyecto,
        @IdEspecialidad,
        @Cotizacion,
        1,
        GETDATE()
    );
END";

        foreach (var item in normalized)
        {
            await db.ExecuteAsync(upsertSql, new
            {
                dto.IdProyecto,
                item.IdEspecialidad,
                item.Cotizacion
            }, tx);
        }

        tx.Commit();
        return Ok(new { ok = true, totalCotizacionMateriales = normalized.Sum(x => x.Cotizacion) });
    }

    private static List<CotizacionMaterialRow> FindCotizacionesForEspecialidad(
        FacturadoMaterialRow facturado,
        IReadOnlyCollection<CotizacionMaterialRow> cotizaciones)
    {
        var matches = new Dictionary<int, CotizacionMaterialRow>();
        var rawEspecialidad = facturado.Especialidad ?? string.Empty;
        var rawKey = NormalizeKey(rawEspecialidad);
        var parts = SplitEspecialidades(rawEspecialidad)
            .Select(NormalizeKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        void Add(CotizacionMaterialRow row)
        {
            if (row.Cotizacion == 0m) return;
            matches[row.IdEspecialidad] = row;
        }

        var byId = cotizaciones.FirstOrDefault(x => x.IdEspecialidad == facturado.IdEspecialidad);
        if (byId is not null) Add(byId);

        foreach (var cotizacion in cotizaciones)
        {
            var cotKey = NormalizeKey(cotizacion.Especialidad);

            if (cotKey == rawKey)
            {
                Add(cotizacion);
                continue;
            }

            if (parts.Any(part => part == cotKey))
            {
                Add(cotizacion);
                continue;
            }

            if (parts.Any(part => part.Length >= 3 && cotKey.Length >= 3 && (part.Contains(cotKey) || cotKey.Contains(part))))
            {
                Add(cotizacion);
            }
        }

        return matches.Values.ToList();
    }

    private static IEnumerable<string> SplitEspecialidades(string value)
    {
        return (value ?? string.Empty)
            .Split(new[] { ',', ';', '/', '|', '+', '&' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NormalizeKey(string value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Trim()
            .ToUpperInvariant();
    }

    private static async Task EnsureTablesAsync(IDbConnection db)
    {
        await db.ExecuteAsync(@"
IF OBJECT_ID('contable.CotizacionMaterialEspecialidad', 'U') IS NULL
BEGIN
    CREATE TABLE contable.CotizacionMaterialEspecialidad
    (
        IdCotizacionMaterialEspecialidad INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        IdProyecto INT NOT NULL,
        IdEspecialidad INT NOT NULL,
        Cotizacion DECIMAL(18,2) NOT NULL CONSTRAINT DF_CotizacionMaterialEspecialidad_Cotizacion DEFAULT(0),
        Activo BIT NOT NULL CONSTRAINT DF_CotizacionMaterialEspecialidad_Activo DEFAULT(1),
        FechaCreacion DATETIME NOT NULL CONSTRAINT DF_CotizacionMaterialEspecialidad_FechaCreacion DEFAULT(GETDATE()),
        FechaActualizacion DATETIME NULL,
        CONSTRAINT FK_CotizacionMaterialEspecialidad_Proyecto FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto),
        CONSTRAINT FK_CotizacionMaterialEspecialidad_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES maestra.Especialidad(IdEspecialidad)
    );
    CREATE UNIQUE INDEX UX_CotizacionMaterialEspecialidad_Proyecto_Especialidad
        ON contable.CotizacionMaterialEspecialidad(IdProyecto, IdEspecialidad);
END;
");
    }

    private sealed class CotizacionMaterialRow
    {
        public int IdEspecialidad { get; set; }
        public string Especialidad { get; set; } = string.Empty;
        public decimal Cotizacion { get; set; }
    }

    private sealed class FacturadoMaterialRow
    {
        public int IdEspecialidad { get; set; }
        public string Especialidad { get; set; } = string.Empty;
        public decimal Facturado { get; set; }
    }

    private sealed class CotizacionMaterialesResumenItem
    {
        public int IdEspecialidad { get; set; }
        public string Especialidad { get; set; } = string.Empty;
        public decimal Cotizacion { get; set; }
        public decimal Facturado { get; set; }
        public decimal Saldo { get; set; }
    }
}
