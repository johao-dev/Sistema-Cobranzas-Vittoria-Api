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
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Persistence.Repository;

/// <summary>
/// Adaptador Dapper de gastos directos sobre contable.usp_GastoDirecto_*. Traduce los rechazos del
/// rango 515xx (formato 'CODIGO: detalle') a GastoDirectoException, sin reinterpretar la regla.
/// </summary>
public sealed class GastoDirectoRepository : RepositoryBase, IGastoDirectoRepository
{
    private readonly IUsuarioActualService _usuario;

    public GastoDirectoRepository(IDbConnectionFactory factory, IUsuarioActualService usuario) : base(factory)
        => _usuario = usuario;

    public Task<IReadOnlyList<GastoDirecto>> ListarAsync(FiltroGastosDirectos f) => Ejecutar<IReadOnlyList<GastoDirecto>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<GastoDirectoEntity>("contable.usp_GastoDirecto_Listar",
            new
            {
                f.Estado,
                f.IdProveedor,
                f.IdCentroCosto,
                f.Desde,
                f.Hasta,
                IdsCategoriaGasto = f.IdsCategoriaGasto is null ? null : string.Join(',', f.IdsCategoriaGasto)
            },
            commandType: CommandType.StoredProcedure);
        return filas.Select(GastoDirectoMapper.ToDomain).ToList();
    });

    public Task<IReadOnlyList<CentroCostoGastoDirecto>> ListarCentrosCostoAsync() => Ejecutar<IReadOnlyList<CentroCostoGastoDirecto>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<CentroCostoGastoDirectoEntity>("contable.usp_GastoDirecto_CentrosCosto",
            commandType: CommandType.StoredProcedure);
        return filas.Select(GastoDirectoMapper.ToDomain).ToList();
    });

    public Task<IReadOnlyList<ProveedorGastoDirecto>> ListarProveedoresAsync() => Ejecutar<IReadOnlyList<ProveedorGastoDirecto>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<ProveedorGastoDirectoEntity>("contable.usp_GastoDirecto_Proveedores",
            commandType: CommandType.StoredProcedure);
        return filas.Select(GastoDirectoMapper.ToDomain).ToList();
    });

    public Task<IReadOnlyList<CategoriaGastoDirecto>> ListarCategoriasAsync() => Ejecutar<IReadOnlyList<CategoriaGastoDirecto>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<CategoriaGastoDirectoEntity>("contable.usp_GastoDirecto_Categorias",
            commandType: CommandType.StoredProcedure);
        return filas.Select(GastoDirectoMapper.ToDomain).ToList();
    });

    public Task<IReadOnlyList<PartidaDisponibleGasto>> ListarPartidasDisponiblesAsync(int idCentroCosto)
        => Ejecutar<IReadOnlyList<PartidaDisponibleGasto>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<PartidaDisponibleGastoEntity>("contable.usp_GastoDirecto_PartidasDisponibles",
            new { IdCentroCosto = idCentroCosto }, commandType: CommandType.StoredProcedure);
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
        var parametros = Parametros(r);
        parametros.Add("UsuarioCreacion", _usuario.ObtenerUsuarioActual());
        return await db.ExecuteScalarAsync<int>("contable.usp_GastoDirecto_Crear", parametros,
            commandType: CommandType.StoredProcedure);
    });

    /// <summary>
    /// Revalida las relaciones bajo SERIALIZABLE antes de insertar. Todos los gastos y sus referencias
    /// utilizan los mismos SPs del alta manual, una conexión y una transacción; ningún SP hace commit.
    /// </summary>
    public async Task CrearLoteAsync(IReadOnlyList<GastoDirectoImportado> lote, string usuario, CancellationToken ct = default)
    {
        using var db = Open();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        CommandDefinition Comando(string sp, object? parametros = null) => new(sp, parametros, tx,
            commandType: CommandType.StoredProcedure, cancellationToken: ct);
        var errores = new List<DetalleErrorFila>();
        var centros = (await db.QueryAsync<CentroCostoGastoDirectoEntity>(Comando("contable.usp_GastoDirecto_CentrosCosto")))
            .Select(c => c.IdCentroCosto).ToHashSet();
        var categorias = (await db.QueryAsync<CategoriaGastoDirectoEntity>(Comando("contable.usp_GastoDirecto_Categorias")))
            .Select(c => c.IdCategoriaGasto).ToHashSet();
        var proveedores = (await db.QueryAsync<ProveedorGastoDirectoEntity>(Comando("contable.usp_GastoDirecto_Proveedores")))
            .Select(p => p.IdProveedor).ToHashSet();
        var monedas = (await db.QueryAsync<int>(Comando("ControlPresupuestario.usp_Moneda_Listar", new { Activo = true }))).ToHashSet();
        var partidas = new Dictionary<int, IReadOnlyList<PartidaDisponibleGastoEntity>>();
        foreach (var centro in lote.Select(i => i.IdCentroCosto).Distinct())
            partidas[centro] = (await db.QueryAsync<PartidaDisponibleGastoEntity>(
                Comando("contable.usp_GastoDirecto_PartidasDisponibles", new { IdCentroCosto = centro }))).ToArray();
        foreach (var item in lote)
        {
            var r = item.Registro;
            void Error(string campo, string mensaje) => errores.Add(new(item.Fila, campo, "REGLA_NEGOCIO",
                $"Fila {item.Fila}: {mensaje}"));
            if (!centros.Contains(item.IdCentroCosto)) Error("Centro de costo", "el centro dejó de estar habilitado.");
            if (!categorias.Contains(r.IdCategoriaGasto)) Error("Categoría código", "la categoría dejó de estar activa.");
            if (r.IdProveedor is null || !proveedores.Contains(r.IdProveedor.Value)) Error("Proveedor", "el proveedor dejó de estar activo.");
            if (!partidas[item.IdCentroCosto].Any(p => p.IdPresupuestoDetalle == r.IdPresupuestoDetalle && p.IdMoneda == r.IdMoneda))
                Error("Partida", "la partida ya no está disponible en la versión aprobada del centro de costo.");
            if (!monedas.Contains(r.IdMoneda)) Error("Partida", "la moneda del presupuesto dejó de estar activa.");
            if (r.MonedaReferencia is { } referencia && !monedas.Contains(referencia.IdMonedaOriginal))
                Error("Moneda factura", "la moneda de la factura dejó de estar activa.");
        }
        if (errores.Count > 0)
            throw new DatosInvalidosException("Las relaciones del lote cambiaron. No se importó ningún gasto.", errores, lote.Count);

        var actual = lote.FirstOrDefault();
        var campoActual = "Gasto";
        try
        {
            foreach (var item in lote)
            {
                actual = item;
                campoActual = "Gasto";
                var parametros = Parametros(item.Registro);
                parametros.Add("UsuarioCreacion", usuario);
                var id = await db.ExecuteScalarAsync<int>(Comando("contable.usp_GastoDirecto_Crear", parametros));
                campoActual = "Factura(s)";
                foreach (var factura in item.Facturas)
                    await db.ExecuteAsync(Comando("contable.usp_GastoDirectoDocumento_Registrar", new
                    {
                        IdGastoDirecto = id, TipoDocumento = Domain.ValueObject.TipoDocumentoGasto.Factura,
                        NombreArchivo = factura, RutaArchivo = (string?)null, Extension = (string?)null
                    }));
            }
            ct.ThrowIfCancellationRequested();
            tx.Commit();
        }
        catch (SqlException ex) when (ex.Number is >= 51500 and <= 51599)
        {
            // Dispose revierte también las referencias de filas anteriores, incluso con XACT_ABORT.
            var separador = ex.Message.IndexOf(':');
            var codigo = separador > 0 ? ex.Message[..separador].Trim() : "REGLA_NEGOCIO";
            var mensaje = separador > 0 ? ex.Message[(separador + 1)..].Trim() : "El procedimiento rechazó el registro.";
            throw new DatosInvalidosException("El lote fue revertido. No se importó ningún gasto.",
                new[] { new DetalleErrorFila(actual!.Fila, campoActual, codigo, $"Fila {actual.Fila}: {mensaje}") }, lote.Count);
        }
    }

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
        p.Add("IdCategoriaGasto", r.IdCategoriaGasto);
        p.Add("IdProveedor", r.IdProveedor);
        p.Add("IdMoneda", r.IdMoneda);
        p.Add("Fecha", r.Fecha);
        p.Add("Concepto", r.Concepto);
        p.Add("Descripcion", r.Descripcion);
        p.Add("Monto", r.Monto);
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
