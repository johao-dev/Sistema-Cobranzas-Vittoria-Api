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
    pr.NombreProyecto AS Proyecto,
    ISNULL(SUM(d.Soles), 0) AS TotalPresupuesto
FROM contable.PresupuestoProyecto p
INNER JOIN maestra.Proyecto pr ON pr.IdProyecto = p.IdProyecto
LEFT JOIN contable.PresupuestoProyectoDetalle d ON d.IdPresupuestoProyecto = p.IdPresupuestoProyecto
WHERE p.IdProyecto = @IdProyecto AND p.Activo = 1
GROUP BY p.IdPresupuestoProyecto, p.IdProyecto, pr.NombreProyecto;", new { IdProyecto = idProyecto });

            if (header == null)
                return Ok(null);

            var items = (await db.QueryAsync(@"
SELECT
    d.IdPresupuestoProyectoDetalle,
    d.Concepto,
    d.Soles,
    d.Incidencia
FROM contable.PresupuestoProyectoDetalle d
INNER JOIN contable.PresupuestoProyecto p ON p.IdPresupuestoProyecto = d.IdPresupuestoProyecto
WHERE p.IdProyecto = @IdProyecto AND p.Activo = 1
ORDER BY d.IdPresupuestoProyectoDetalle;", new { IdProyecto = idProyecto })).ToList();

            var totalCompras = await db.ExecuteScalarAsync<decimal>(@"
SELECT CAST(ISNULL(SUM(c.MontoTotal), 0) AS DECIMAL(18,2))
FROM compras.Compra c
INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
LEFT JOIN compras.Requerimiento rq ON rq.IdRequerimiento = oc.IdRequerimiento
WHERE COALESCE(oc.IdProyecto, rq.IdProyecto) = @IdProyecto;", new { IdProyecto = idProyecto });

            var totalPresupuesto = (decimal?)header.TotalPresupuesto ?? 0m;
            var saldo = totalPresupuesto - totalCompras;

            return Ok(new
            {
                idPresupuesto = (int?)header.IdPresupuestoProyecto,
                idProyecto = (int?)header.IdProyecto,
                proyecto = (string?)header.Proyecto,
                totalPresupuesto,
                totalCompras,
                saldo,
                items = items.Select(x => new
                {
                    idPresupuestoDetalle = (int?)x.IdPresupuestoProyectoDetalle,
                    concepto = (string?)x.Concepto ?? string.Empty,
                    soles = (decimal?)x.Soles ?? 0m,
                    incidencia = (decimal?)x.Incidencia ?? 0m
                })
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
    Concepto,
    Soles,
    Incidencia
)
VALUES
(
    @IdPresupuestoProyecto,
    @Concepto,
    @Soles,
    @Incidencia
);";

            foreach (var item in dto.Items)
            {
                await db.ExecuteAsync(sqlInsertDetalle, new
                {
                    IdPresupuestoProyecto = idPresupuesto.Value,
                    Concepto = (item.Concepto ?? string.Empty).Trim(),
                    Soles = item.Soles,
                    Incidencia = item.Incidencia
                }, tx);
            }

            tx.Commit();
            return Ok(new { ok = true, idPresupuesto = idPresupuesto.Value });
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
        FechaActualizacion DATETIME NOT NULL CONSTRAINT DF_PresupuestoProyecto_FechaActualizacion DEFAULT(GETDATE())
    );

    ALTER TABLE contable.PresupuestoProyecto
    ADD CONSTRAINT FK_PresupuestoProyecto_Proyecto
        FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto);

    CREATE UNIQUE INDEX UX_PresupuestoProyecto_IdProyecto
        ON contable.PresupuestoProyecto(IdProyecto);
END;

IF OBJECT_ID('contable.PresupuestoProyectoDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE contable.PresupuestoProyectoDetalle
    (
        IdPresupuestoProyectoDetalle INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        IdPresupuestoProyecto INT NOT NULL,
        Concepto NVARCHAR(200) NOT NULL,
        Soles DECIMAL(18,2) NOT NULL,
        Incidencia DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoProyectoDetalle_Incidencia DEFAULT(0)
    );

    ALTER TABLE contable.PresupuestoProyectoDetalle
    ADD CONSTRAINT FK_PresupuestoProyectoDetalle_PresupuestoProyecto
        FOREIGN KEY (IdPresupuestoProyecto) REFERENCES contable.PresupuestoProyecto(IdPresupuestoProyecto);
END;

IF COL_LENGTH('contable.PresupuestoProyecto', 'PresupuestoInicial') IS NOT NULL
BEGIN
    ALTER TABLE contable.PresupuestoProyecto DROP COLUMN PresupuestoInicial;
END;");
        }
    }
}
