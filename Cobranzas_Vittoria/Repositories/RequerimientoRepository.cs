using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Application.Compras.Excepciones;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Repositories
{
    public class RequerimientoRepository : RepositoryBase, IRequerimientoRepository
    {
        public RequerimientoRepository(IDbConnectionFactory factory) : base(factory) { }

        public async Task<IEnumerable<Requerimiento>> ListAsync(string? estado, int? idEspecialidad, int? idProyecto)
        {
            using var db = Open();

            const string sql = @"
SELECT
    r.IdRequerimiento,
    r.NumeroRequerimiento,
    r.FechaRequerimiento,
    r.IdEspecialidad,
    eb.Nombre AS Especialidad,
    x.Especialidades,
    r.IdProyecto,
    p.NombreProyecto,
    r.Descripcion,
    r.FechaEntrega,
    r.IdUsuarioSolicitante,
    LTRIM(RTRIM(CONCAT(u.Nombres, ' ', ISNULL(u.Apellidos, '')))) AS Solicitante,
    r.Estado,
    r.Observacion,
    r.FechaCreacion
FROM compras.Requerimiento r
INNER JOIN maestra.Especialidad eb ON eb.IdEspecialidad = r.IdEspecialidad
INNER JOIN maestra.Proyecto p ON p.IdProyecto = r.IdProyecto
INNER JOIN seguridad.Usuario u ON u.IdUsuario = r.IdUsuarioSolicitante
OUTER APPLY (
    SELECT STRING_AGG(q.Nombre, ', ') AS Especialidades
    FROM (
        SELECT DISTINCT e2.Nombre
        FROM compras.RequerimientoDetalle rd2
        INNER JOIN maestra.Material m2 ON m2.IdMaterial = rd2.IdMaterial
        INNER JOIN maestra.Especialidad e2 ON e2.IdEspecialidad = m2.IdEspecialidad
        WHERE rd2.IdRequerimiento = r.IdRequerimiento
    ) q
) x
WHERE (@Estado IS NULL OR @Estado = '' OR r.Estado = @Estado)
  AND (@IdProyecto IS NULL OR r.IdProyecto = @IdProyecto)
  AND (@IdEspecialidad IS NULL OR EXISTS (
        SELECT 1
        FROM compras.RequerimientoDetalle rd2
        INNER JOIN maestra.Material m2 ON m2.IdMaterial = rd2.IdMaterial
        WHERE rd2.IdRequerimiento = r.IdRequerimiento
          AND m2.IdEspecialidad = @IdEspecialidad
  ))
GROUP BY
    x.Especialidades,
    r.IdRequerimiento,
    r.NumeroRequerimiento,
    r.FechaRequerimiento,
    r.IdEspecialidad,
    eb.Nombre,
    r.IdProyecto,
    p.NombreProyecto,
    r.Descripcion,
    r.FechaEntrega,
    r.IdUsuarioSolicitante,
    u.Nombres,
    u.Apellidos,
    r.Estado,
    r.Observacion,
    r.FechaCreacion
ORDER BY r.IdRequerimiento DESC;";

            return await db.QueryAsync<Requerimiento>(sql, new { Estado = estado, IdEspecialidad = idEspecialidad, IdProyecto = idProyecto });
        }

        public async Task<(Requerimiento? head, List<RequerimientoDetalle> items, List<RequerimientoValidacion> validaciones)> GetAsync(int idRequerimiento)
        {
            using var db = Open();

            const string sqlHead = @"
SELECT
    r.IdRequerimiento,
    r.NumeroRequerimiento,
    r.FechaRequerimiento,
    r.IdEspecialidad,
    eb.Nombre AS Especialidad,
    x.Especialidades,
    r.IdProyecto,
    p.NombreProyecto,
    r.Descripcion,
    r.FechaEntrega,
    r.IdUsuarioSolicitante,
    LTRIM(RTRIM(CONCAT(u.Nombres, ' ', ISNULL(u.Apellidos, '')))) AS Solicitante,
    r.Estado,
    r.Observacion,
    r.FechaCreacion
FROM compras.Requerimiento r
INNER JOIN maestra.Especialidad eb ON eb.IdEspecialidad = r.IdEspecialidad
INNER JOIN maestra.Proyecto p ON p.IdProyecto = r.IdProyecto
INNER JOIN seguridad.Usuario u ON u.IdUsuario = r.IdUsuarioSolicitante
OUTER APPLY (
    SELECT STRING_AGG(es.Nombre, ', ') AS Especialidades
    FROM (
        SELECT DISTINCT e2.Nombre
        FROM compras.RequerimientoDetalle rd2
        INNER JOIN maestra.Material m2 ON m2.IdMaterial = rd2.IdMaterial
        INNER JOIN maestra.Especialidad e2 ON e2.IdEspecialidad = m2.IdEspecialidad
        WHERE rd2.IdRequerimiento = r.IdRequerimiento
    ) es
) x
WHERE r.IdRequerimiento = @IdRequerimiento;";

            const string sqlItems = @"
SELECT
    rd.IdRequerimientoDetalle,
    rd.IdRequerimiento,
    rd.IdMaterial,
    m.IdEspecialidad,
    e.Nombre AS Especialidad,
    m.Descripcion AS Material,
    m.UnidadMedida,
    rd.Cantidad,
    rd.Observacion
FROM compras.RequerimientoDetalle rd
INNER JOIN maestra.Material m ON m.IdMaterial = rd.IdMaterial
INNER JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
WHERE rd.IdRequerimiento = @IdRequerimiento
ORDER BY rd.IdRequerimientoDetalle;";

            const string sqlValidaciones = @"
SELECT *
FROM compras.RequerimientoValidacion
WHERE IdRequerimiento = @IdRequerimiento
ORDER BY FechaValidacion DESC;";

            var head = await db.QueryFirstOrDefaultAsync<Requerimiento>(sqlHead, new { IdRequerimiento = idRequerimiento });
            var items = (await db.QueryAsync<RequerimientoDetalle>(sqlItems, new { IdRequerimiento = idRequerimiento })).AsList();
            var validaciones = (await db.QueryAsync<RequerimientoValidacion>(sqlValidaciones, new { IdRequerimiento = idRequerimiento })).AsList();

            return (head, items, validaciones);
        }

        public async Task<int> CrearAsync(RequerimientoCreateDto dto)
        {
            using var db = Open();

            var numeroRequerimiento = await EnsureNumeroRequerimientoAsync(db, (dto.NumeroRequerimiento ?? string.Empty).Trim());
            var fechaRequerimiento = dto.FechaRequerimiento == default ? DateTime.Today : dto.FechaRequerimiento.Date;

            var tvp = new DataTable();
            tvp.Columns.Add("IdMaterial", typeof(int));
            tvp.Columns.Add("Cantidad", typeof(decimal));
            tvp.Columns.Add("Observacion", typeof(string));

            foreach (var it in dto.Items)
                tvp.Rows.Add(it.IdMaterial, it.Cantidad, (object?)it.Observacion ?? DBNull.Value);

            var p = new DynamicParameters();
            p.Add("NumeroRequerimiento", numeroRequerimiento);
            p.Add("FechaRequerimiento", fechaRequerimiento);
            p.Add("IdEspecialidad", dto.IdEspecialidad);
            p.Add("IdProyecto", dto.IdProyecto);
            p.Add("Descripcion", dto.Descripcion);
            p.Add("FechaEntrega", dto.FechaEntrega);
            p.Add("IdUsuarioSolicitante", dto.IdUsuarioSolicitante);
            p.Add("Observacion", dto.Observacion);
            p.Add("Items", tvp.AsTableValuedParameter("compras.TVP_RequerimientoDetalle"));

            return await db.ExecuteScalarAsync<int>(
                "compras.usp_Requerimiento_Crear",
                p,
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<bool> PuedeEditarAsync(int idRequerimiento)
        {
            using var db = Open();

            const string sql = @"
SELECT CASE
    WHEN EXISTS (
        SELECT 1
        FROM compras.OrdenCompra oc
        WHERE oc.IdRequerimiento = @IdRequerimiento
    ) THEN CAST(0 AS bit)
    WHEN EXISTS (
        SELECT 1
        FROM compras.Requerimiento r
        WHERE r.IdRequerimiento = @IdRequerimiento
          AND UPPER(ISNULL(r.Estado,'')) <> 'REGISTRADO'
    ) THEN CAST(0 AS bit)
    ELSE CAST(1 AS bit)
END";

            return await db.ExecuteScalarAsync<bool>(sql, new { IdRequerimiento = idRequerimiento });
        }

        public async Task UpdateAsync(int idRequerimiento, RequerimientoUpdateDto dto)
        {
            using var db = Open();

            var puedeEditar = await PuedeEditarAsync(idRequerimiento);
            if (!puedeEditar)
                throw new InvalidOperationException("El requerimiento ya no puede modificarse porque ya fue enviado a orden de compra o ya tiene una orden asociada.");

            var tvp = new DataTable();
            tvp.Columns.Add("IdMaterial", typeof(int));
            tvp.Columns.Add("Cantidad", typeof(decimal));
            tvp.Columns.Add("Observacion", typeof(string));

            foreach (var it in dto.Items)
                tvp.Rows.Add(it.IdMaterial, it.Cantidad, (object?)it.Observacion ?? DBNull.Value);

            var p = new DynamicParameters();
            p.Add("IdRequerimiento", idRequerimiento);
            p.Add("NumeroRequerimiento", dto.NumeroRequerimiento);
            p.Add("FechaRequerimiento", dto.FechaRequerimiento);
            p.Add("IdEspecialidad", dto.IdEspecialidad);
            p.Add("IdProyecto", dto.IdProyecto);
            p.Add("Descripcion", dto.Descripcion);
            p.Add("FechaEntrega", dto.FechaEntrega);
            p.Add("IdUsuarioSolicitante", dto.IdUsuarioSolicitante);
            p.Add("Observacion", dto.Observacion);
            p.Add("Items", tvp.AsTableValuedParameter("compras.TVP_RequerimientoDetalle"));

            await db.ExecuteAsync(
                "compras.usp_Requerimiento_Actualizar",
                p,
                commandType: CommandType.StoredProcedure
            );
        }

        private async Task<string> EnsureNumeroRequerimientoAsync(IDbConnection db, string numeroSolicitado)
        {
            if (!string.IsNullOrWhiteSpace(numeroSolicitado))
            {
                var existe = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM compras.Requerimiento WHERE NumeroRequerimiento = @NumeroRequerimiento",
                    new { NumeroRequerimiento = numeroSolicitado });

                if (existe == 0)
                    return numeroSolicitado;
            }

            var siguiente = await db.QuerySingleAsync<int>(@"
SELECT ISNULL(MAX(TRY_CONVERT(INT, NumeroRequerimiento)), 0) + 1
FROM compras.Requerimiento;");

            return siguiente.ToString();
        }
        public async Task<bool> EsSolicitanteAsync(int idRequerimiento, int idUsuario)
        {
            using var db = Open();
            return await db.ExecuteScalarAsync<bool>(@"
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM compras.Requerimiento
                    WHERE IdRequerimiento = @IdRequerimiento AND IdUsuarioSolicitante = @IdUsuario
                    ) THEN 1 ELSE 0 END AS bit);",
                new { IdRequerimiento = idRequerimiento, IdUsuario = idUsuario });
        }

        public Task EnviarAsync(int idRequerimiento, int idUsuario, string? observacion)
            => EjecutarTransicionAsync("compras.usp_Requerimiento_Enviar", idRequerimiento, idUsuario, observacion);

        public Task ProcesarStockAsync(int idRequerimiento, int idUsuario, string resultado, string? observacion)
            => EjecutarTransicionAsync("compras.usp_Requerimiento_ProcesarStock", idRequerimiento, idUsuario, observacion, resultado);

        public Task AprobarAsync(int idRequerimiento, int idUsuario, string? observacion)
            => EjecutarTransicionAsync("compras.usp_Requerimiento_Aprobar", idRequerimiento, idUsuario, observacion);

        public Task RechazarAsync(int idRequerimiento, int idUsuario, string? observacion)
            => EjecutarTransicionAsync("compras.usp_Requerimiento_Rechazar", idRequerimiento, idUsuario, observacion);

        public Task EnviarComprasAsync(int idRequerimiento, int idUsuario, string? observacion)
            => EjecutarTransicionAsync("compras.usp_Requerimiento_EnviarCompras", idRequerimiento, idUsuario, observacion);

        // Se podría refactorizar usando el patrón de diseño Execute Around, encapsulando la apertura de la conexión,
        // la ejecución del procedimiento almacenado y el manejo de excepciones en un único método.
        private async Task EjecutarTransicionAsync(
            string procedimiento,
            int idRequerimiento,
            int idUsuario,
            string? observacion,
            string? resultado = null)
        {
            using var db = Open();
            try
            {
                var parametros = new DynamicParameters();
                parametros.Add("IdRequerimiento", idRequerimiento);
                parametros.Add("IdUsuario", idUsuario);
                parametros.Add("Observacion", observacion);
                if (resultado is not null)
                    parametros.Add("Resultado", resultado);

                await db.ExecuteAsync(
                    procedimiento,
                    parametros,
                    commandType: CommandType.StoredProcedure);
            }
            catch (SqlException ex) when (ex.Number is >= 51051 and <= 51060)
            {
                throw new ValidacionNegocioComprasException(
                    "estado",
                    "REQUERIMIENTO_TRANSICION_INVALIDA",
                    ex.Message);
            }
        }
    }
}
