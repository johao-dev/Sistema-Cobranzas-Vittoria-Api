using System.Data;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Domain.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Inventario.Infrastructure.Persistence;
using Cobranzas_Vittoria.Repositories;
using Dapper;

namespace Cobranzas_Vittoria.Inventario.Infrastructure.Persistence.Repository;

/// <summary>Persistencia Dapper del Kardex; traduce los rechazos SQL del módulo.</summary>
public sealed class KardexEntradaRepository : RepositoryBase, IKardexEntradaRepository
{
    public KardexEntradaRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IReadOnlyList<KardexEntrada>> ListarAsync(
        FiltroKardex filtro,
        CancellationToken ct = default)
    {
        return await TraductorErroresSql.EjecutarAsync(async () =>
        {
            using IDbConnection db = Open();
            // Dapper no soporta DateOnly como parametro de Stored Procedure en
            // Microsoft.Data.SqlClient 6.x: lo convertimos a DateTime para que
            // el driver lo serialice como DATE en SQL Server.
            IEnumerable<KardexEntrada> result = await db.QueryAsync<KardexEntrada>(
                new CommandDefinition(
                    commandText: "almacen.usp_KardexEntrada_Listar",
                    parameters: new
                    {
                        IdEspecialidad = filtro.IdEspecialidad,
                        IdProyecto = filtro.IdProyecto,
                        IdProveedor = filtro.IdProveedor,
                        FechaDesde = filtro.FechaDesde?.ToDateTime(TimeOnly.MinValue),
                        FechaHasta = filtro.FechaHasta?.ToDateTime(TimeOnly.MinValue)
                    },
                    cancellationToken: ct,
                    commandType: CommandType.StoredProcedure));
            return result.AsList();
        }, "entrada");
    }

    public async Task<KardexEntrada> RegistrarAsync(
        RegistroKardexEntrada dto,
        CancellationToken ct = default)
    {
        return await TraductorErroresSql.EjecutarAsync(async () =>
        {
            using IDbConnection db = Open();
            KardexEntrada result = await db.QueryFirstAsync<KardexEntrada>(
                new CommandDefinition(
                    commandText: "almacen.usp_KardexEntrada_Registrar",
                    parameters: new
                    {
                        IdMaterial = dto.IdMaterial,
                        IdProveedor = dto.IdProveedor,
                        IdProyecto = dto.IdProyecto,
                        NumeroDocumento = dto.NumeroDocumento,
                        Fecha = dto.Fecha.ToDateTime(TimeOnly.MinValue),
                        Cantidad = dto.Cantidad,
                        Observacion = dto.Observacion
                    },
                    cancellationToken: ct,
                    commandType: CommandType.StoredProcedure));
            return result;
        }, "entrada");
    }

    public async Task<KardexEntrada> ActualizarAsync(
        RegistroKardexEntrada dto,
        CancellationToken ct = default)
    {
        return await TraductorErroresSql.EjecutarAsync(async () =>
        {
            using IDbConnection db = Open();
            KardexEntrada result = await db.QueryFirstAsync<KardexEntrada>(
                new CommandDefinition(
                    commandText: "almacen.usp_KardexEntrada_Actualizar",
                    parameters: new
                    {
                        IdKardexEntrada = dto.IdKardexEntrada,
                        IdMaterial = dto.IdMaterial,
                        IdProveedor = dto.IdProveedor,
                        IdProyecto = dto.IdProyecto,
                        NumeroDocumento = dto.NumeroDocumento,
                        Fecha = dto.Fecha.ToDateTime(TimeOnly.MinValue),
                        Cantidad = dto.Cantidad,
                        Observacion = dto.Observacion
                    },
                    cancellationToken: ct,
                    commandType: CommandType.StoredProcedure));
            return result;
        }, "entrada");
    }

    public async Task EliminarAsync(int idKardexEntrada, CancellationToken ct = default)
    {
        await TraductorErroresSql.EjecutarAsync(async () =>
        {
            using IDbConnection db = Open();
            await db.ExecuteAsync(
                new CommandDefinition(
                    commandText: "almacen.usp_KardexEntrada_Eliminar",
                    parameters: new { IdKardexEntrada = idKardexEntrada },
                    cancellationToken: ct,
                    commandType: CommandType.StoredProcedure));
        }, "entrada");
    }
}
