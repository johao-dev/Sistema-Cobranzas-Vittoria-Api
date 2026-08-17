using System.Data;
using Cobranzas_Vittoria.Application.Inventario.Dtos;
using Cobranzas_Vittoria.Application.Inventario.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Dapper;

namespace Cobranzas_Vittoria.Infrastructure.Repositories.Inventario;

/// <summary>
/// Implementacion de <see cref="IKardexEntradaRepository"/> con Dapper + SQL Server.
///
/// <para>
/// <b>Patron de ejecucion</b>: hereda de <see cref="RepositoryBase"/> para reusar
/// la apertura de conexiones. La transaccion la controla el SP internamente
/// (<c>SET XACT_ABORT ON</c> + <c>BEGIN TRAN</c> + <c>COMMIT/ROLLBACK</c>),
/// por lo que el repositorio NO abre transacciones adicionales.
/// </para>
///
/// <para>
/// <b>Por que hereda de <c>RepositoryBase</c> (legacy)</b>:
/// <see cref="RepositoryBase"/> es estable y usado por todos los repos legacy.
/// Crear un nuevo <c>AbstractRepository</c> solo para Inventario duplicaria
/// logica trivial. Esto esta documentado como deuda tecnica en
/// <c>project_memory.md</c>; cuando se migren los repos legacy a
/// <c>Application/</c> se homogeniza la base.
/// </para>
///
/// <para>
/// <b>Mapeo de parametros</b>: Dapper convierte el nombre del parametro del
/// SP sin el <c>@</c> a una propiedad del objeto anonimo. La convencion es
/// usar nombres PascalCase en el anonimo y los parametros del SP en
/// PascalCase precedidos de <c>@</c> (Dapper los une correctamente).
/// </para>
///
/// <para>
/// <b>Errores</b>: este repository propaga <see cref="Microsoft.Data.SqlClient.SqlException"/> sin
/// modificar. La traduccion a <c>DetalleErrorValidacion</c> + HTTP 422 la
/// hace el <c>KardexInventarioService</c> usando
/// <c>Application/Common/SqlExceptionTranslator</c>.
/// </para>
/// </summary>
public sealed class KardexEntradaRepository : RepositoryBase, IKardexEntradaRepository
{
    public KardexEntradaRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IReadOnlyList<KardexEntradaResponseDto>> ListarAsync(
        KardexFiltroInventarioDto filtro,
        CancellationToken ct = default)
    {
        using var db = Open();
        var result = await db.QueryAsync<KardexEntradaResponseDto>(
            new CommandDefinition(
                commandText: "almacen.usp_KardexEntrada_Listar",
                parameters: new
                {
                    IdEspecialidad = filtro.IdEspecialidad,
                    IdProyecto = filtro.IdProyecto,
                    IdProveedor = filtro.IdProveedor,
                    FechaDesde = filtro.FechaDesde,
                    FechaHasta = filtro.FechaHasta
                },
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure));
        return result.AsList();
    }

    public async Task<KardexEntradaResponseDto> RegistrarAsync(
        KardexEntradaCreateDto dto,
        CancellationToken ct = default)
    {
        using var db = Open();
        var result = await db.QueryFirstAsync<KardexEntradaResponseDto>(
            new CommandDefinition(
                commandText: "almacen.usp_KardexEntrada_Registrar",
                parameters: new
                {
                    IdEspecialidad = dto.IdEspecialidad,
                    IdMaterial = dto.IdMaterial,
                    IdProveedor = dto.IdProveedor,
                    IdProyecto = dto.IdProyecto,
                    NumeroDocumento = dto.NumeroDocumento,
                    Fecha = dto.Fecha,
                    Cantidad = dto.Cantidad,
                    Observacion = dto.Observacion
                },
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure));
        return result;
    }

    public async Task<KardexEntradaResponseDto> ActualizarAsync(
        KardexEntradaCreateDto dto,
        CancellationToken ct = default)
    {
        using var db = Open();
        var result = await db.QueryFirstAsync<KardexEntradaResponseDto>(
            new CommandDefinition(
                commandText: "almacen.usp_KardexEntrada_Actualizar",
                parameters: new
                {
                    IdKardexEntrada = dto.IdKardexEntrada,
                    IdEspecialidad = dto.IdEspecialidad,
                    IdMaterial = dto.IdMaterial,
                    IdProveedor = dto.IdProveedor,
                    IdProyecto = dto.IdProyecto,
                    NumeroDocumento = dto.NumeroDocumento,
                    Fecha = dto.Fecha,
                    Cantidad = dto.Cantidad,
                    Observacion = dto.Observacion
                },
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure));
        return result;
    }

    public async Task EliminarAsync(int idKardexEntrada, CancellationToken ct = default)
    {
        using var db = Open();
        await db.ExecuteAsync(
            new CommandDefinition(
                commandText: "almacen.usp_KardexEntrada_Eliminar",
                parameters: new { IdKardexEntrada = idKardexEntrada },
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure));
    }
}
