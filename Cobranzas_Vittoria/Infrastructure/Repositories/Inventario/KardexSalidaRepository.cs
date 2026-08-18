using System.Data;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Application.Inventario.Dtos;
using Cobranzas_Vittoria.Application.Inventario.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Dapper;

namespace Cobranzas_Vittoria.Infrastructure.Repositories.Inventario;

/// <summary>
/// Implementacion de <see cref="IKardexSalidaRepository"/> con Dapper + SQL Server.
///
/// <para>
/// <b>Patron TVP</b>: las salidas manuales tienen 1..N items. Los items se
/// envian al SP como un Table-Valued Parameter (<c>almacen.TVP_KardexSalidaItem</c>).
/// La conversion de <c>List&lt;KardexSalidaItemCreateDto&gt;</c> a
/// <see cref="DataTable"/> la hace <see cref="TvpMapper"/> (helper de
/// <c>Application/Importacion/Persistence/</c>) — se reusa, no se duplica.
/// </para>
///
/// <para>
/// <b>Por que la conversion de DTO a TVP vive en el repository y no en el
/// service</b>: mantiene al service libre de dependencias con
/// <see cref="DataTable"/>. El service solo conoce DTOs; el repository
/// es la frontera con la tecnologia de persistencia.
/// </para>
///
/// <para>
/// <b>Una fila por item en el SELECT</b>: el SP devuelve N filas (una por
/// cada item) repitiendo la cabecera. <c>QueryAsync&lt;T&gt;</c> las mapea
/// a una <c>List&lt;KardexSalidaResponseDto&gt;</c> con N entradas; el
/// controller las devuelve tal cual para que el front pueda renderizar
/// una tabla plana o agruparlas por <c>IdKardexSalida</c>.
/// </para>
///
/// <para>
/// <b>Por que el resultado de Registrar/Actualizar es
/// <c>IReadOnlyList&lt;KardexSalidaResponseDto&gt;</c> y no un unico objeto</b>:
/// para que la respuesta tenga la misma forma que el GET (un row por item).
/// El front puede usar la misma rutina de render.
/// </para>
///
/// <para>
/// <b>Errores</b>: este repository propaga <see cref="Microsoft.Data.SqlClient.SqlException"/> sin
/// modificar. La traduccion a HTTP 422 la hace el service.
/// </para>
/// </summary>
public sealed class KardexSalidaRepository : RepositoryBase, IKardexSalidaRepository
{
    /// <summary>Nombre completo del TVP usado por los SPs de KardexSalida.</summary>
    private const string TvpKardexSalidaItem = "almacen.TVP_KardexSalidaItem";

    public KardexSalidaRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IReadOnlyList<KardexSalidaResponseDto>> ListarAsync(
        KardexFiltroInventarioDto filtro,
        CancellationToken ct = default)
    {
        using var db = Open();
        // Dapper no soporta DateOnly como parametro de Stored Procedure en
        // Microsoft.Data.SqlClient 6.x: lo convertimos a DateTime para que
        // el driver lo serialice como DATE en SQL Server.
        var result = await db.QueryAsync<KardexSalidaResponseDto>(
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
    }

    public async Task<IReadOnlyList<KardexSalidaResponseDto>> RegistrarAsync(
        KardexSalidaCreateDto dto,
        CancellationToken ct = default)
    {
        var items = (dto.Items ?? Enumerable.Empty<KardexSalidaItemCreateDto>()).ToList();
        var dataTable = TvpMapper.ToDataTable(items);

        using var db = Open();

        // DynamicParameters para controlar el SqlDbType.Object del TVP.
        // El resto de parametros se mergean con AddDynamicParams.
        var parameters = new DynamicParameters();
        parameters.Add(
            name: "@Items",
            value: dataTable.AsTableValuedParameter(TvpKardexSalidaItem),
            dbType: DbType.Object,
            direction: ParameterDirection.Input);
        parameters.AddDynamicParams(new
        {
            IdEspecialidad = dto.IdEspecialidad,
            IdProyecto = dto.IdProyecto,
            NumeroDocumento = dto.NumeroDocumento,
            Fecha = dto.Fecha.ToDateTime(TimeOnly.MinValue),
            Solicitante = dto.Solicitante,
            Observacion = dto.Observacion
        });

        var result = await db.QueryAsync<KardexSalidaResponseDto>(
            new CommandDefinition(
                commandText: "almacen.usp_KardexSalida_Registrar",
                parameters: parameters,
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure));
        return result.AsList();
    }

    public async Task<IReadOnlyList<KardexSalidaResponseDto>> ActualizarAsync(
        KardexSalidaCreateDto dto,
        CancellationToken ct = default)
    {
        var items = (dto.Items ?? Enumerable.Empty<KardexSalidaItemCreateDto>()).ToList();
        var dataTable = TvpMapper.ToDataTable(items);

        using var db = Open();

        var parameters = new DynamicParameters();
        parameters.Add(
            name: "@Items",
            value: dataTable.AsTableValuedParameter(TvpKardexSalidaItem),
            dbType: DbType.Object,
            direction: ParameterDirection.Input);
        parameters.AddDynamicParams(new
        {
            IdKardexSalida = dto.IdKardexSalida,
            IdEspecialidad = dto.IdEspecialidad,
            IdProyecto = dto.IdProyecto,
            NumeroDocumento = dto.NumeroDocumento,
            Fecha = dto.Fecha.ToDateTime(TimeOnly.MinValue),
            Solicitante = dto.Solicitante,
            Observacion = dto.Observacion
        });

        var result = await db.QueryAsync<KardexSalidaResponseDto>(
            new CommandDefinition(
                commandText: "almacen.usp_KardexSalida_Actualizar",
                parameters: parameters,
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure));
        return result.AsList();
    }

    public async Task EliminarAsync(int idKardexSalida, CancellationToken ct = default)
    {
        using var db = Open();
        await db.ExecuteAsync(
            new CommandDefinition(
                commandText: "almacen.usp_KardexSalida_Eliminar",
                parameters: new { IdKardexSalida = idKardexSalida },
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure));
    }
}
