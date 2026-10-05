using System.Data;
using System.Text.RegularExpressions;
using Cobranzas_Vittoria.Application.Importacion;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Repository;

/// <summary>Adaptador Dapper de las partidas de una versión sobre los SPs usp_PresupuestoDetalle_*.</summary>
public sealed class PresupuestoDetalleRepository : RepositoryBase, IPresupuestoDetalleRepository
{
    private const string Schema = "ControlPresupuestario.";

    public PresupuestoDetalleRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<PresupuestoDetalle>> ListarPorVersionAsync(int idPresupuestoVersion)
        => TraductorErroresSql.EjecutarAsync<IReadOnlyList<PresupuestoDetalle>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<PresupuestoDetalleEntity>(Schema + "usp_PresupuestoDetalle_ListarPorVersion",
            new { IdPresupuestoVersion = idPresupuestoVersion }, commandType: CommandType.StoredProcedure);
        return filas.Select(PresupuestoDetalleMapper.ToDomain).ToList();
    });

    public Task<int> AgregarAsync(PresupuestoDetalle d) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(Schema + "usp_PresupuestoDetalle_Agregar",
            new { d.IdPresupuestoVersion, d.IdCatalogoPartida, d.MontoPresupuestado, d.Observacion },
            commandType: CommandType.StoredProcedure);
    });

    public Task ActualizarAsync(PresupuestoDetalle d) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        await db.ExecuteAsync(Schema + "usp_PresupuestoDetalle_Actualizar",
            new { d.IdPresupuestoDetalle, d.MontoPresupuestado, d.Observacion },
            commandType: CommandType.StoredProcedure);
    });

    public Task EliminarAsync(int idPresupuestoDetalle) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        await db.ExecuteAsync(Schema + "usp_PresupuestoDetalle_Eliminar",
            new { IdPresupuestoDetalle = idPresupuestoDetalle }, commandType: CommandType.StoredProcedure);
    });

    public Task<ResultadoCargaLote> CargarLoteAsync(LotePresupuestario lote) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        // Mismo orden de columnas que ControlPresupuestario.TVP_PresupuestoDetalleLote.
        var tabla = new DataTable();
        tabla.Columns.Add("IdCatalogoPartida", typeof(int));
        tabla.Columns.Add("MontoPresupuestado", typeof(decimal));
        tabla.Columns.Add("Observacion", typeof(string));
        tabla.Columns.Add("_Fila", typeof(int));
        foreach (var item in lote.Items)
            tabla.Rows.Add(item.IdCatalogoPartida, item.MontoPresupuestado, (object?)item.Observacion ?? DBNull.Value, item.Fila);

        using var db = Open();
        var fila = await db.QueryFirstAsync<CargaLoteEntity>(Schema + "usp_PresupuestoDetalle_CargaLote",
            new
            {
                lote.IdPresupuestoVersion,
                Detalles = tabla.AsTableValuedParameter("ControlPresupuestario.TVP_PresupuestoDetalleLote"),
                lote.QuitarAusentes
            }, commandType: CommandType.StoredProcedure);
        return PresupuestoDetalleMapper.ToDomain(fila);
    });

    public Task<ResultadoImportacionEstructura> ImportarEstructuraAsync(ImportacionEstructura importacion)
        => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        try
        {
            var creadas = 0;
            if (importacion.PartidasNuevas.Count > 0)
            {
                // Mismo orden de columnas que ControlPresupuestario.TVP_CatalogoPartida.
                var nuevas = new DataTable();
                nuevas.Columns.Add("Codigo", typeof(string));
                nuevas.Columns.Add("Nombre", typeof(string));
                nuevas.Columns.Add("IdTipoPartida", typeof(int));
                nuevas.Columns.Add("CodigoPadre", typeof(string));
                nuevas.Columns.Add("IdSeccionGasto", typeof(int));
                nuevas.Columns.Add("Descripcion", typeof(string));
                nuevas.Columns.Add("_Fila", typeof(int));
                foreach (var p in importacion.PartidasNuevas)
                    nuevas.Rows.Add(p.Codigo, p.Nombre, p.IdTipoPartida, (object?)p.CodigoPadre ?? DBNull.Value,
                        (object?)p.IdSeccionGasto ?? DBNull.Value, DBNull.Value, p.Fila);
                try
                {
                    creadas = await db.QueryFirstAsync<int>(Schema + "usp_CatalogoPartida_CargaMasiva",
                        new { Filas = nuevas.AsTableValuedParameter("ControlPresupuestario.TVP_CatalogoPartida"), importacion.Usuario },
                        tx, commandType: CommandType.StoredProcedure);
                }
                catch (SqlException ex) when (ex.Number is >= 50001 and <= 50099)
                {
                    throw RechazoCatalogo(ex);
                }
            }

            var codigos = importacion.Montos.Select(m => m.CodigoPartida).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var ids = (await db.QueryAsync<(int Id, string Codigo)>(
                    "SELECT IdCatalogoPartida, Codigo FROM ControlPresupuestario.CatalogoPartida WHERE Codigo IN @codigos",
                    new { codigos }, tx))
                .ToDictionary(p => p.Codigo, p => p.Id, StringComparer.OrdinalIgnoreCase);

            // Mismo orden de columnas que ControlPresupuestario.TVP_PresupuestoDetalleLote.
            var lote = new DataTable();
            lote.Columns.Add("IdCatalogoPartida", typeof(int));
            lote.Columns.Add("MontoPresupuestado", typeof(decimal));
            lote.Columns.Add("Observacion", typeof(string));
            lote.Columns.Add("_Fila", typeof(int));
            foreach (var m in importacion.Montos)
                lote.Rows.Add(ids[m.CodigoPartida], m.Monto, (object?)m.Observacion ?? DBNull.Value, m.Fila);

            var fila = await db.QueryFirstAsync<CargaLoteEntity>(Schema + "usp_PresupuestoDetalle_CargaLote",
                new
                {
                    importacion.IdPresupuestoVersion,
                    Detalles = lote.AsTableValuedParameter("ControlPresupuestario.TVP_PresupuestoDetalleLote"),
                    importacion.QuitarAusentes
                }, tx, commandType: CommandType.StoredProcedure);
            tx.Commit();
            return new ResultadoImportacionEstructura(PresupuestoDetalleMapper.ToDomain(fila), creadas);
        }
        catch
        {
            if (tx.Connection is not null) tx.Rollback();
            throw;
        }
    });

    /// <summary>Los rechazos del SP de catálogo traen 'CODIGO: fila N, detalle': se informan como error de esa fila.</summary>
    private static DatosInvalidosException RechazoCatalogo(SqlException ex)
    {
        var separador = ex.Message.IndexOf(':');
        var codigo = separador > 0 ? ex.Message[..separador].Trim() : CodigosError.Fila.ReglaNegocio;
        var mensaje = separador > 0 ? ex.Message[(separador + 1)..].Trim() : ex.Message;
        var numero = Regex.Match(mensaje, @"^fila (\d+),\s*");
        var filaError = numero.Success ? int.Parse(numero.Groups[1].Value) : 0;
        if (numero.Success)
            mensaje = char.ToUpperInvariant(mensaje[numero.Length]) + mensaje[(numero.Length + 1)..];
        return new DatosInvalidosException("El catálogo rechazó las partidas nuevas. No se cargó nada.",
            new[] { new DetalleErrorFila(filaError, "Codigo", codigo, mensaje) });
    }
}
