using System.Data;
using Cobranzas_Vittoria.Application.Inventario.Dtos;
using Cobranzas_Vittoria.Application.Inventario.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Dapper;

namespace Cobranzas_Vittoria.Infrastructure.Repositories.Inventario;

/// <summary>
/// Implementacion de <see cref="IKardexStockRepository"/> con Dapper + SQL Server.
///
/// <para>
/// <b>Por que este repositorio es de SOLO LECTURA</b>:
/// el stock-actual se calcula y mantiene transitivamente por los SPs
/// <c>usp_KardexEntrada_*</c> y <c>usp_KardexSalida_*</c> dentro de la
/// misma transaccion que origina el movimiento. No existe una operacion
/// "set stock = N" expuesta al API. Por eso este repositorio solo expone
/// <c>ListarAsync</c>.
/// </para>
///
/// <para>
/// <b>Por que NO usa la vista <c>vw_Kardex_StockActual_v2</c></b>:
/// el SP <c>usp_Kardex_StockActual_Listar</c> ya inline los JOINs a
/// maestra y ordena por Especialidad + Material. Consumir directamente
/// el SP evita un nivel de indireccion sin valor aqui. La vista queda
/// disponible para queries ad-hoc del equipo de BI.
/// </para>
///
/// <para>
/// <b>Filtros soportados</b>: <c>IdEspecialidad</c>, <c>FechaDesde</c> y
/// <c>FechaHasta</c>. El stock es global por (IdMaterial, IdEspecialidad);
/// no hay filtro por proyecto.
/// </para>
///
/// <para>
/// <b>Por que hereda de <c>RepositoryBase</c> (legacy)</b>:
/// mismo motivo que <c>KardexEntradaRepository</c> y
/// <c>KardexSalidaRepository</c>: <see cref="RepositoryBase"/> es estable
/// y usado por todos los repos legacy. Esto esta documentado como
/// deuda tecnica en <c>project_memory.md</c>.
/// </para>
///
/// <para>
/// <b>Errores</b>: este repository propaga <see cref="Microsoft.Data.SqlClient.SqlException"/>
/// sin modificar. La traduccion a HTTP 422 la hace el
/// <c>KardexInventarioService</c> usando
/// <c>Application/Common/SqlExceptionTranslator</c>.
/// </para>
/// </summary>
public sealed class KardexStockRepository : RepositoryBase, IKardexStockRepository
{
    public KardexStockRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IReadOnlyList<KardexStockActualResponseDto>> ListarAsync(
        KardexStockFiltroInventarioDto filtro,
        CancellationToken ct = default)
    {
        filtro ??= new KardexStockFiltroInventarioDto();

        using var db = Open();
        // Dapper no soporta DateOnly como parametro de Stored Procedure en
        // Microsoft.Data.SqlClient 6.x: lo convertimos a DateTime para que
        // el driver lo serialice como DATE en SQL Server.
        var result = await db.QueryAsync<KardexStockActualResponseDto>(
            new CommandDefinition(
                commandText: "almacen.usp_Kardex_StockActual_Listar",
                parameters: new
                {
                    IdEspecialidad = filtro.IdEspecialidad,
                    FechaDesde = filtro.FechaDesde?.ToDateTime(TimeOnly.MinValue),
                    FechaHasta = filtro.FechaHasta?.ToDateTime(TimeOnly.MinValue)
                },
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure));
        return result.AsList();
    }
}
