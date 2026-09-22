using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories;

public sealed class GastoDirectoRepository : RepositoryBase, IGastoDirectoRepository
{
    public GastoDirectoRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<GastoDirecto>> ListarAsync(string? estado, int? idProveedor,
        int? idCentroCosto, DateTime? desde, DateTime? hasta)
    {
        using var db = Open();
        return await db.QueryAsync<GastoDirecto>("contable.usp_GastoDirecto_Listar",
            new { Estado = estado, IdProveedor = idProveedor, IdCentroCosto = idCentroCosto,
                Desde = desde?.Date, Hasta = hasta?.Date }, commandType: CommandType.StoredProcedure);
    }

    public async Task<(GastoDirecto? Gasto, IReadOnlyList<GastoDirectoDocumento> Documentos)> ObtenerAsync(int id)
    {
        using var db = Open();
        using var result = await db.QueryMultipleAsync("contable.usp_GastoDirecto_Obtener",
            new { IdGastoDirecto = id }, commandType: CommandType.StoredProcedure);
        var gasto = await result.ReadFirstOrDefaultAsync<GastoDirecto>();
        var documentos = (await result.ReadAsync<GastoDirectoDocumento>()).AsList();
        return (gasto, documentos);
    }

    public async Task<int> CrearAsync(GastoDirectoUpsertDto dto)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>("contable.usp_GastoDirecto_Crear", Parametros(dto),
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> ActualizarAsync(int id, GastoDirectoUpsertDto dto)
    {
        using var db = Open();
        var parametros = Parametros(dto);
        parametros.Add("IdGastoDirecto", id);
        return await db.ExecuteScalarAsync<int>("contable.usp_GastoDirecto_Actualizar", parametros,
            commandType: CommandType.StoredProcedure);
    }

    public async Task ConfirmarAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("contable.usp_GastoDirecto_Confirmar",
            new { IdGastoDirecto = id }, commandType: CommandType.StoredProcedure);
    }

    public async Task AnularAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("contable.usp_GastoDirecto_Anular",
            new { IdGastoDirecto = id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<GastoDirectoDocumento>> ListarDocumentosAsync(int id)
    {
        var (_, documentos) = await ObtenerAsync(id);
        return documentos;
    }

    public async Task RegistrarDocumentosAsync(int id, IReadOnlyCollection<GastoDirectoDocumentoNuevo> documentos)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        try
        {
            foreach (var documento in documentos)
            {
                await db.ExecuteAsync("contable.usp_GastoDirectoDocumento_Registrar",
                    new
                    {
                        IdGastoDirecto = id,
                        documento.TipoDocumento,
                        documento.NombreArchivo,
                        documento.RutaArchivo,
                        documento.Extension
                    }, tx, commandType: CommandType.StoredProcedure);
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static DynamicParameters Parametros(GastoDirectoUpsertDto dto)
    {
        var p = new DynamicParameters();
        p.Add("IdPresupuestoDetalle", dto.IdPresupuestoDetalle);
        p.Add("IdProveedor", dto.IdProveedor);
        p.Add("IdMoneda", dto.IdMoneda);
        p.Add("Fecha", dto.Fecha.Date);
        p.Add("Concepto", dto.Concepto?.Trim());
        p.Add("Descripcion", string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim());
        p.Add("Monto", decimal.Round(dto.Monto, 2, MidpointRounding.AwayFromZero));
        return p;
    }
}
