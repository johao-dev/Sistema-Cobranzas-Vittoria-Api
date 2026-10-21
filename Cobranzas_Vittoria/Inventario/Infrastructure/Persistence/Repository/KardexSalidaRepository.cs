using System.Data;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Domain.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Inventario.Infrastructure.Persistence;
using Cobranzas_Vittoria.Repositories;
using Dapper;

namespace Cobranzas_Vittoria.Inventario.Infrastructure.Persistence.Repository;

/// <summary>Persistencia Dapper del Kardex; traduce los rechazos SQL del módulo.</summary>
public sealed class KardexSalidaRepository : RepositoryBase, IKardexSalidaRepository
{
    /// <summary>Nombre completo del TVP usado por los SPs de KardexSalida.</summary>
    private const string TvpKardexSalidaItem = "almacen.TVP_KardexSalidaItem";

    public KardexSalidaRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IReadOnlyList<KardexSalida>> ListarAsync(
        FiltroKardex filtro,
        CancellationToken ct = default)
    {
        return await TraductorErroresSql.EjecutarAsync(async () =>
        {
            using IDbConnection db = Open();
            // Dapper no soporta DateOnly como parametro de Stored Procedure en
            // Microsoft.Data.SqlClient 6.x: lo convertimos a DateTime para que
            // el driver lo serialice como DATE en SQL Server.
            IEnumerable<KardexSalida> result = await db.QueryAsync<KardexSalida>(
                new CommandDefinition(
                    commandText: "almacen.usp_KardexSalida_Listar",
                    parameters: new
                    {
                        IdEspecialidad = filtro.IdEspecialidad,
                        IdProyecto = filtro.IdProyecto,
                        FechaDesde = filtro.FechaDesde?.ToDateTime(TimeOnly.MinValue),
                        FechaHasta = filtro.FechaHasta?.ToDateTime(TimeOnly.MinValue)
                    },
                    cancellationToken: ct,
                    commandType: CommandType.StoredProcedure));
            return result.AsList();
        }, "salida");
    }

    public async Task<IReadOnlyList<KardexSalida>> RegistrarAsync(
        RegistroKardexSalida dto,
        CancellationToken ct = default)
    {
        return await TraductorErroresSql.EjecutarAsync(async () =>
        {
            List<ItemKardexSalida> items = (dto.Items ?? Enumerable.Empty<ItemKardexSalida>()).ToList();
            DataTable dataTable = TvpMapper.ToDataTable(items);

            using IDbConnection db = Open();

            // DynamicParameters para controlar el SqlDbType.Object del TVP.
            // El resto de parametros se mergean con AddDynamicParams.
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add(
                name: "@Items",
                value: dataTable.AsTableValuedParameter(TvpKardexSalidaItem),
                dbType: DbType.Object,
                direction: ParameterDirection.Input);
            parameters.AddDynamicParams(new
            {
                IdProyecto = dto.IdProyecto,
                NumeroDocumento = dto.NumeroDocumento,
                Fecha = dto.Fecha.ToDateTime(TimeOnly.MinValue),
                Solicitante = dto.Solicitante,
                Observacion = dto.Observacion
            });

            IEnumerable<KardexSalida> result = await db.QueryAsync<KardexSalida>(
                new CommandDefinition(
                    commandText: "almacen.usp_KardexSalida_Registrar",
                    parameters: parameters,
                    cancellationToken: ct,
                    commandType: CommandType.StoredProcedure));
            return result.AsList();
        }, "salida");
    }

    public async Task<IReadOnlyList<KardexSalida>> ActualizarAsync(
        RegistroKardexSalida dto,
        CancellationToken ct = default)
    {
        return await TraductorErroresSql.EjecutarAsync(async () =>
        {
            List<ItemKardexSalida> items = (dto.Items ?? Enumerable.Empty<ItemKardexSalida>()).ToList();
            DataTable dataTable = TvpMapper.ToDataTable(items);

            using IDbConnection db = Open();

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add(
                name: "@Items",
                value: dataTable.AsTableValuedParameter(TvpKardexSalidaItem),
                dbType: DbType.Object,
                direction: ParameterDirection.Input);
            parameters.AddDynamicParams(new
            {
                IdKardexSalida = dto.IdKardexSalida,
                IdProyecto = dto.IdProyecto,
                NumeroDocumento = dto.NumeroDocumento,
                Fecha = dto.Fecha.ToDateTime(TimeOnly.MinValue),
                Solicitante = dto.Solicitante,
                Observacion = dto.Observacion
            });

            IEnumerable<KardexSalida> result = await db.QueryAsync<KardexSalida>(
                new CommandDefinition(
                    commandText: "almacen.usp_KardexSalida_Actualizar",
                    parameters: parameters,
                    cancellationToken: ct,
                    commandType: CommandType.StoredProcedure));
            return result.AsList();
        }, "salida");
    }

    public async Task EliminarAsync(int idKardexSalida, CancellationToken ct = default)
    {
        await TraductorErroresSql.EjecutarAsync(async () =>
        {
            using IDbConnection db = Open();
            await db.ExecuteAsync(
                new CommandDefinition(
                    commandText: "almacen.usp_KardexSalida_Eliminar",
                    parameters: new { IdKardexSalida = idKardexSalida },
                    cancellationToken: ct,
                    commandType: CommandType.StoredProcedure));
        }, "salida");
    }
}
