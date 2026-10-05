using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Persistence.Mapper;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Persistence.Repository;

/// <summary>
/// Adaptador Dapper de gastos directos sobre contable.usp_GastoDirecto_*. Traduce los rechazos del
/// rango 515xx (formato 'CODIGO: detalle') a GastoDirectoException, sin reinterpretar la regla.
/// </summary>
public sealed class GastoDirectoRepository : RepositoryBase, IGastoDirectoRepository
{
    public GastoDirectoRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<GastoDirecto>> ListarAsync(FiltroGastosDirectos f) => Ejecutar<IReadOnlyList<GastoDirecto>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<GastoDirectoEntity>("contable.usp_GastoDirecto_Listar",
            new { f.Estado, f.IdProveedor, f.IdCentroCosto, f.Desde, f.Hasta, CodigoSeccion = f.Seccion },
            commandType: CommandType.StoredProcedure);
        return filas.Select(GastoDirectoMapper.ToDomain).ToList();
    });

    public Task<IReadOnlyList<CentroCostoSeccion>> ListarCentrosCostoAsync(string seccion) => Ejecutar<IReadOnlyList<CentroCostoSeccion>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<CentroCostoSeccionEntity>("contable.usp_GastoDirecto_CentrosCostoPorSeccion",
            new { CodigoSeccion = seccion }, commandType: CommandType.StoredProcedure);
        return filas.Select(GastoDirectoMapper.ToDomain).ToList();
    });

    public Task<IReadOnlyList<ProveedorSeccion>> ListarProveedoresAsync(string seccion) => Ejecutar<IReadOnlyList<ProveedorSeccion>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<ProveedorSeccionEntity>("contable.usp_GastoDirecto_ProveedoresPorSeccion",
            new { CodigoSeccion = seccion }, commandType: CommandType.StoredProcedure);
        return filas.Select(GastoDirectoMapper.ToDomain).ToList();
    });

    public Task<IReadOnlyList<PartidaDisponibleGasto>> ListarPartidasDisponiblesAsync(string seccion, int idCentroCosto)
        => Ejecutar<IReadOnlyList<PartidaDisponibleGasto>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<PartidaDisponibleGastoEntity>("contable.usp_GastoDirecto_PartidasDisponibles",
            new { CodigoSeccion = seccion, IdCentroCosto = idCentroCosto }, commandType: CommandType.StoredProcedure);
        return filas.Select(GastoDirectoMapper.ToDomain).ToList();
    });

    public Task<(GastoDirecto? Gasto, IReadOnlyList<GastoDirectoDocumento> Documentos)> ObtenerAsync(int idGastoDirecto)
        => Ejecutar<(GastoDirecto?, IReadOnlyList<GastoDirectoDocumento>)>(async () =>
    {
        using var db = Open();
        using var lector = await db.QueryMultipleAsync("contable.usp_GastoDirecto_Obtener",
            new { IdGastoDirecto = idGastoDirecto }, commandType: CommandType.StoredProcedure);
        var gasto = await lector.ReadFirstOrDefaultAsync<GastoDirectoEntity>();
        var documentos = (await lector.ReadAsync<GastoDirectoDocumentoEntity>()).Select(GastoDirectoMapper.ToDomain).ToList();
        return (gasto is null ? null : GastoDirectoMapper.ToDomain(gasto), documentos);
    });

    public Task<int> CrearAsync(RegistroGastoDirecto r) => Ejecutar(async () =>
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>("contable.usp_GastoDirecto_Crear", Parametros(r),
            commandType: CommandType.StoredProcedure);
    });

    public Task<int> ActualizarAsync(int idGastoDirecto, RegistroGastoDirecto r) => Ejecutar(async () =>
    {
        using var db = Open();
        var parametros = Parametros(r);
        parametros.Add("IdGastoDirecto", idGastoDirecto);
        return await db.ExecuteScalarAsync<int>("contable.usp_GastoDirecto_Actualizar", parametros,
            commandType: CommandType.StoredProcedure);
    });

    public Task ConfirmarAsync(int idGastoDirecto) => Ejecutar(async () =>
    {
        using var db = Open();
        await db.ExecuteAsync("contable.usp_GastoDirecto_Confirmar", new { IdGastoDirecto = idGastoDirecto },
            commandType: CommandType.StoredProcedure);
        return 0;
    });

    public Task AnularAsync(int idGastoDirecto) => Ejecutar(async () =>
    {
        using var db = Open();
        await db.ExecuteAsync("contable.usp_GastoDirecto_Anular", new { IdGastoDirecto = idGastoDirecto },
            commandType: CommandType.StoredProcedure);
        return 0;
    });

    public Task RegistrarDocumentosAsync(int idGastoDirecto, IReadOnlyCollection<DocumentoNuevo> documentos) => Ejecutar(async () =>
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        foreach (var d in documentos)
        {
            await db.ExecuteAsync("contable.usp_GastoDirectoDocumento_Registrar",
                new { IdGastoDirecto = idGastoDirecto, d.TipoDocumento, d.NombreArchivo, d.RutaArchivo, d.Extension },
                tx, commandType: CommandType.StoredProcedure);
        }
        tx.Commit();
        return 0;
    });

    private static DynamicParameters Parametros(RegistroGastoDirecto r)
    {
        var p = new DynamicParameters();
        p.Add("IdPresupuestoDetalle", r.IdPresupuestoDetalle);
        p.Add("IdProveedor", r.IdProveedor);
        p.Add("IdMoneda", r.IdMoneda);
        p.Add("Fecha", r.Fecha);
        p.Add("Concepto", r.Concepto);
        p.Add("Descripcion", r.Descripcion);
        p.Add("Monto", r.Monto);
        p.Add("CodigoSeccion", r.Seccion);
        p.Add("IdMonedaOriginal", r.MonedaReferencia?.IdMonedaOriginal);
        p.Add("MontoOriginal", r.MonedaReferencia?.MontoOriginal);
        p.Add("TipoCambio", r.MonedaReferencia?.TipoCambio);
        p.Add("FechaTipoCambio", r.MonedaReferencia?.FechaTipoCambio);
        return p;
    }

    private static async Task<T> Ejecutar<T>(Func<Task<T>> accion)
    {
        try { return await accion(); }
        catch (SqlException ex) when (ex.Number is >= 51500 and <= 51599)
        {
            var separador = ex.Message.IndexOf(':');
            var codigo = separador > 0 ? ex.Message[..separador].Trim() : "CONFLICTO_NEGOCIO_CONTABLE";
            var mensaje = separador > 0 ? ex.Message[(separador + 1)..].Trim() : "La operación contable fue rechazada.";
            throw new GastoDirectoException(codigo, mensaje);
        }
    }
}
