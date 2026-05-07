using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Contable;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers
{
    [ApiController]
    [Route("api/contable/presupuesto")]
    public class PresupuestoController : ControllerBase
    {
        private readonly IDbConnectionFactory _factory;
        private const decimal TipoCambioDefault = 3.41m;

        private static readonly string[] ConceptosFijos =
        {
            "TERRENO",
            "ALCABALA",
            "CONSTRUCCION (incluir GG e IGV)",
            "UTILIDAD DEL CONSTRUCTOR (en caso de tercerizar la operación)",
            "DEMOLICION",
            "ANTEPROYECTO",
            "PROYECTO",
            "LICENCIA DE CONSTRUCCION",
            "GASTOS ADMINISTRATIVOS",
            "PUBLICIDAD / COMISION POR VENTAS",
            "INSTALACIONES (LUZ Y AGUA)",
            "CONFORMIDAD DE OBRA",
            "DECLARATORIA DE FABRICA",
            "INDEPENDIZACION",
            "OTROS GASTOS"
        };

        public PresupuestoController(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        [HttpGet("{idProyecto:int}")]
        public async Task<IActionResult> GetByProyecto(int idProyecto)
        {
            using var db = _factory.CreateConnection();
            await EnsureTablesAsync(db);

            var header = await db.QueryFirstOrDefaultAsync(@"
SELECT
    p.IdPresupuestoProyecto,
    p.IdProyecto,
    pr.NombreProyecto AS Proyecto
FROM contable.PresupuestoProyecto p
INNER JOIN maestra.Proyecto pr ON pr.IdProyecto = p.IdProyecto
WHERE p.IdProyecto = @IdProyecto AND ISNULL(p.Activo,1) = 1;", new { IdProyecto = idProyecto });

            var manualItems = new Dictionary<string, PresupuestoItemCalc>(StringComparer.OrdinalIgnoreCase);
            if (header != null)
            {
                var detalles = await db.QueryAsync(@"
SELECT
    d.IdPresupuestoProyectoDetalle,
    d.Orden,
    d.Concepto,
    d.Soles,
    ISNULL(d.Dolares, 0) AS Dolares
FROM contable.PresupuestoProyectoDetalle d
INNER JOIN contable.PresupuestoProyecto p ON p.IdPresupuestoProyecto = d.IdPresupuestoProyecto
WHERE p.IdProyecto = @IdProyecto AND ISNULL(p.Activo,1) = 1 AND ISNULL(d.Activo, 1) = 1
ORDER BY ISNULL(d.Orden, d.IdPresupuestoProyectoDetalle), d.IdPresupuestoProyectoDetalle;", new { IdProyecto = idProyecto });

                foreach (var item in detalles)
                {
                    var concepto = ((string?)item.Concepto ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(concepto)) continue;
                    manualItems[NormalizeConcepto(concepto)] = new PresupuestoItemCalc
                    {
                        Concepto = concepto,
                        Soles = (decimal?)item.Soles ?? 0m,
                        Dolares = (decimal?)item.Dolares ?? 0m
                    };
                }
            }

            var autoItems = await GetAutomaticItemsAsync(db, idProyecto);
            foreach (var pair in autoItems)
            {
                manualItems[pair.Key] = pair.Value;
            }

            var items = ConceptosFijos.Select((concepto, index) =>
            {
                var key = NormalizeConcepto(concepto);
                var value = manualItems.TryGetValue(key, out var item)
                    ? item
                    : new PresupuestoItemCalc { Concepto = concepto, Soles = 0m, Dolares = 0m };

                return new
                {
                    idPresupuestoDetalle = (int?)null,
                    orden = index + 1,
                    concepto,
                    soles = decimal.Round(value.Soles, 2),
                    dolares = decimal.Round(value.Dolares, 2),
                    automatico = autoItems.ContainsKey(key)
                };
            }).ToList();

            var totalCompras = await db.ExecuteScalarAsync<decimal>(@"
SELECT CAST(ISNULL(SUM(c.MontoTotal), 0) AS DECIMAL(18,2))
FROM compras.Compra c
INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
LEFT JOIN compras.Requerimiento rq ON rq.IdRequerimiento = oc.IdRequerimiento
WHERE COALESCE(oc.IdProyecto, rq.IdProyecto) = @IdProyecto;", new { IdProyecto = idProyecto });

            var totalPresupuesto = items.Sum(x => x.soles);
            var saldo = decimal.Round(totalPresupuesto - totalCompras, 2);
            var proyecto = header != null
                ? (string?)header.Proyecto
                : await db.ExecuteScalarAsync<string?>("SELECT TOP 1 NombreProyecto FROM maestra.Proyecto WHERE IdProyecto = @IdProyecto;", new { IdProyecto = idProyecto });

            return Ok(new
            {
                idPresupuesto = header == null ? (int?)null : (int?)header.IdPresupuestoProyecto,
                idProyecto,
                proyecto,
                totalPresupuesto,
                totalCompras,
                saldo,
                items
            });
        }

        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] PresupuestoProyectoUpsertDto dto)
        {
            if (dto.IdProyecto <= 0)
                return BadRequest(new { message = "Debes seleccionar un proyecto válido." });

            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest(new { message = "Debes registrar al menos un ítem de presupuesto." });

            if (dto.Items.Any(x => string.IsNullOrWhiteSpace(x.Concepto)))
                return BadRequest(new { message = "Todos los ítems deben tener concepto." });

            using var db = _factory.CreateConnection();
            await EnsureTablesAsync(db);
            using var tx = db.BeginTransaction();

            var idPresupuesto = await db.ExecuteScalarAsync<int?>(@"
SELECT IdPresupuestoProyecto
FROM contable.PresupuestoProyecto
WHERE IdProyecto = @IdProyecto;", new { dto.IdProyecto }, tx);

            if (idPresupuesto.HasValue)
            {
                await db.ExecuteAsync(@"
UPDATE contable.PresupuestoProyecto
SET Activo = 1,
    FechaActualizacion = GETDATE()
WHERE IdPresupuestoProyecto = @IdPresupuestoProyecto;", new { IdPresupuestoProyecto = idPresupuesto.Value }, tx);

                await db.ExecuteAsync(@"
DELETE FROM contable.PresupuestoProyectoDetalle
WHERE IdPresupuestoProyecto = @IdPresupuestoProyecto;", new { IdPresupuestoProyecto = idPresupuesto.Value }, tx);
            }
            else
            {
                idPresupuesto = await db.ExecuteScalarAsync<int>(@"
INSERT INTO contable.PresupuestoProyecto
(
    IdProyecto,
    Activo,
    FechaCreacion,
    FechaActualizacion
)
VALUES
(
    @IdProyecto,
    1,
    GETDATE(),
    GETDATE()
);
SELECT CAST(SCOPE_IDENTITY() AS INT);", new { dto.IdProyecto }, tx);
            }

            const string sqlInsertDetalle = @"
INSERT INTO contable.PresupuestoProyectoDetalle
(
    IdPresupuestoProyecto,
    Orden,
    Concepto,
    Soles,
    Dolares,
    Activo,
    FechaCreacion
)
VALUES
(
    @IdPresupuestoProyecto,
    @Orden,
    @Concepto,
    @Soles,
    @Dolares,
    1,
    GETDATE()
);";

            var normalized = dto.Items.Select((item, index) => new
            {
                Orden = index + 1,
                Concepto = (item.Concepto ?? string.Empty).Trim().ToUpperInvariant(),
                Soles = decimal.Round(item.Soles, 2),
                Dolares = decimal.Round(item.Dolares, 2)
            }).ToList();

            foreach (var item in normalized)
            {
                await db.ExecuteAsync(sqlInsertDetalle, new
                {
                    IdPresupuestoProyecto = idPresupuesto!.Value,
                    item.Orden,
                    item.Concepto,
                    item.Soles,
                    item.Dolares
                }, tx);
            }

            tx.Commit();
            return Ok(new { ok = true, idPresupuesto = idPresupuesto.Value });
        }

        private static async Task<Dictionary<string, PresupuestoItemCalc>> GetAutomaticItemsAsync(IDbConnection db, int idProyecto)
        {
            var result = new Dictionary<string, PresupuestoItemCalc>(StringComparer.OrdinalIgnoreCase);

            var gastosProyecto = await db.QueryAsync(@"
SELECT
    TipoModulo,
    Concepto,
    CAST(ISNULL(SUM(MontoSoles), 0) AS DECIMAL(18,2)) AS Soles,
    CAST(ISNULL(SUM(MontoDolares), 0) AS DECIMAL(18,2)) AS Dolares
FROM contable.GastoProyecto
WHERE IdProyecto = @IdProyecto
  AND ISNULL(Activo, 1) = 1
  AND Estado = 'Activo'
GROUP BY TipoModulo, Concepto;", new { IdProyecto = idProyecto });

            decimal GetSoles(string tipoModulo, params string[] conceptos) => gastosProyecto
                .Where(x => string.Equals((string)x.TipoModulo, tipoModulo, StringComparison.OrdinalIgnoreCase)
                         && conceptos.Any(c => string.Equals((string)x.Concepto, c, StringComparison.OrdinalIgnoreCase)))
                .Sum(x => (decimal?)x.Soles ?? 0m);

            decimal GetDolares(string tipoModulo, params string[] conceptos) => gastosProyecto
                .Where(x => string.Equals((string)x.TipoModulo, tipoModulo, StringComparison.OrdinalIgnoreCase)
                         && conceptos.Any(c => string.Equals((string)x.Concepto, c, StringComparison.OrdinalIgnoreCase)))
                .Sum(x => (decimal?)x.Dolares ?? 0m);

            void Add(string concepto, decimal soles, decimal dolares = 0m)
            {
                result[NormalizeConcepto(concepto)] = new PresupuestoItemCalc
                {
                    Concepto = concepto,
                    Soles = decimal.Round(soles, 2),
                    Dolares = decimal.Round(dolares, 2)
                };
            }

            var terrenoSoles = GetSoles("Terreno", "TERRENO");
            var terrenoDolares = GetDolares("Terreno", "TERRENO");
            var alcabalaSoles = GetSoles("Terreno", "ALCABALA");
            var alcabalaDolares = GetDolares("Terreno", "ALCABALA");
            var anteproyectoSoles = GetSoles("Terreno", "ANTEPROYECTO");
            var anteproyectoDolares = GetDolares("Terreno", "ANTEPROYECTO");
            var proyectoSoles = GetSoles("Terreno", "PROYECTO");
            var proyectoDolares = GetDolares("Terreno", "PROYECTO");

            Add("TERRENO", terrenoSoles, terrenoDolares);
            Add("ALCABALA", alcabalaSoles, alcabalaDolares);
            Add("ANTEPROYECTO", anteproyectoSoles, anteproyectoDolares);
            Add("PROYECTO", proyectoSoles, proyectoDolares);
            Add("LICENCIA DE CONSTRUCCION", proyectoSoles + anteproyectoSoles, proyectoDolares + anteproyectoDolares);

            var gastosAdmin = await db.QueryFirstOrDefaultAsync(@"
SELECT
    CAST(ISNULL(SUM(CASE WHEN UPPER(ISNULL(Moneda,'PEN')) = 'USD' THEN Monto * @TipoCambio ELSE Monto END), 0) AS DECIMAL(18,2)) AS Soles,
    CAST(ISNULL(SUM(CASE WHEN UPPER(ISNULL(Moneda,'PEN')) = 'USD' THEN Monto ELSE 0 END), 0) AS DECIMAL(18,2)) AS Dolares
FROM contable.GastoAdministrativo
WHERE IdProyecto = @IdProyecto AND ISNULL(Activo,1) = 1;", new { IdProyecto = idProyecto, TipoCambio = TipoCambioDefault });
            Add("GASTOS ADMINISTRATIVOS", (decimal?)gastosAdmin?.Soles ?? 0m, (decimal?)gastosAdmin?.Dolares ?? 0m);

            Add("PUBLICIDAD / COMISION POR VENTAS", GetSoles("Marketing", "PUBLICIDAD", "COMISION POR VENTAS", "COMISIÓN POR VENTAS"), GetDolares("Marketing", "PUBLICIDAD", "COMISION POR VENTAS", "COMISIÓN POR VENTAS"));
            Add("OTROS GASTOS", GetSoles("OtrosGastos", "OTROS GASTOS"), GetDolares("OtrosGastos", "OTROS GASTOS"));
            Add("INDEPENDIZACION", GetSoles("GastosMunicipales", "INDEPENDIZACION", "INDEPENDIZACIÓN"), GetDolares("GastosMunicipales", "INDEPENDIZACION", "INDEPENDIZACIÓN"));
            Add("DECLARATORIA DE FABRICA", GetSoles("GastosMunicipales", "DECLARATORIA", "DECLARATORIA DE FABRICA", "DECLARATORIA DE FÁBRICA"), GetDolares("GastosMunicipales", "DECLARATORIA", "DECLARATORIA DE FABRICA", "DECLARATORIA DE FÁBRICA"));
            Add("CONFORMIDAD DE OBRA", GetSoles("GastosMunicipales", "CONFORMIDAD", "CONFORMIDAD DE OBRA"), GetDolares("GastosMunicipales", "CONFORMIDAD", "CONFORMIDAD DE OBRA"));
            Add("INSTALACIONES (LUZ Y AGUA)", GetSoles("GastosMunicipales", "INSTALACIONES", "INSTALACIONES (LUZ Y AGUA)"), GetDolares("GastosMunicipales", "INSTALACIONES", "INSTALACIONES (LUZ Y AGUA)"));

            return result;
        }

        private static string NormalizeConcepto(string value)
            => (value ?? string.Empty).Trim().ToUpperInvariant();

        private sealed class PresupuestoItemCalc
        {
            public string Concepto { get; set; } = string.Empty;
            public decimal Soles { get; set; }
            public decimal Dolares { get; set; }
        }

        private static async Task EnsureTablesAsync(IDbConnection db)
        {
            await db.ExecuteAsync(@"
IF OBJECT_ID('contable.PresupuestoProyecto', 'U') IS NULL
BEGIN
    CREATE TABLE contable.PresupuestoProyecto
    (
        IdPresupuestoProyecto INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        IdProyecto INT NOT NULL,
        Activo BIT NOT NULL CONSTRAINT DF_PresupuestoProyecto_Activo DEFAULT(1),
        FechaCreacion DATETIME NOT NULL CONSTRAINT DF_PresupuestoProyecto_FechaCreacion DEFAULT(GETDATE()),
        FechaActualizacion DATETIME NULL,
        CONSTRAINT FK_PresupuestoProyecto_Proyecto FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto)
    );
    CREATE UNIQUE INDEX UX_PresupuestoProyecto_IdProyecto ON contable.PresupuestoProyecto(IdProyecto);
END;

IF OBJECT_ID('contable.PresupuestoProyectoDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE contable.PresupuestoProyectoDetalle
    (
        IdPresupuestoProyectoDetalle INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        IdPresupuestoProyecto INT NOT NULL,
        Orden INT NOT NULL,
        Concepto NVARCHAR(250) NOT NULL,
        Soles DECIMAL(18,2) NOT NULL,
        Dolares DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoProyectoDetalle_Dolares DEFAULT(0),
        Activo BIT NOT NULL CONSTRAINT DF_PresupuestoProyectoDetalle_Activo DEFAULT(1),
        FechaCreacion DATETIME NOT NULL CONSTRAINT DF_PresupuestoProyectoDetalle_FechaCreacion DEFAULT(GETDATE()),
        CONSTRAINT FK_PresupuestoProyectoDetalle_PresupuestoProyecto FOREIGN KEY (IdPresupuestoProyecto) REFERENCES contable.PresupuestoProyecto(IdPresupuestoProyecto)
    );
END;

IF COL_LENGTH('contable.PresupuestoProyectoDetalle', 'Orden') IS NULL
BEGIN
    ALTER TABLE contable.PresupuestoProyectoDetalle ADD Orden INT NULL;
END;

IF COL_LENGTH('contable.PresupuestoProyectoDetalle', 'Dolares') IS NULL
BEGIN
    ALTER TABLE contable.PresupuestoProyectoDetalle ADD Dolares DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoProyectoDetalle_Dolares_MIG DEFAULT(0);
END;

IF COL_LENGTH('contable.PresupuestoProyectoDetalle', 'Activo') IS NULL
BEGIN
    ALTER TABLE contable.PresupuestoProyectoDetalle ADD Activo BIT NOT NULL CONSTRAINT DF_PresupuestoProyectoDetalle_Activo_MIG DEFAULT(1);
END;

IF COL_LENGTH('contable.PresupuestoProyectoDetalle', 'FechaCreacion') IS NULL
BEGIN
    ALTER TABLE contable.PresupuestoProyectoDetalle ADD FechaCreacion DATETIME NOT NULL CONSTRAINT DF_PresupuestoProyectoDetalle_FechaCreacion_MIG DEFAULT(GETDATE());
END;
");
        }
    }
}
