using System.Data;
using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Domain.Persistence;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;
using Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Mapper;
using Cobranzas_Vittoria.Data;
using Dapper;

namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Repository;

public sealed class RequerimientoRepository(IDbConnectionFactory factory) : IRequerimientoRepository
{
    static RequerimientoRepository() => SqlMapper.AddTypeHandler(new FechaSqlHandler());

    public Task<Requerimiento?> ObtenerConItemsAsync(int idRequerimiento, CancellationToken cancellationToken = default)
        => TraductorErroresSql.EjecutarAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using IDbConnection db = factory.CreateConnection();

            using SqlMapper.GridReader filas = await db.QueryMultipleAsync(new CommandDefinition("compras.usp_Requerimiento_ObtenerConItems",
                new { IdRequerimiento = idRequerimiento }, commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            RequerimientoEntity? cabecera = await filas.ReadSingleOrDefaultAsync<RequerimientoEntity>();
            IEnumerable<RequerimientoDetalleEntity> items = await filas.ReadAsync<RequerimientoDetalleEntity>();

            return cabecera is null ? null : RequerimientoMapper.ToDomain(cabecera, items);
        });

    public Task<ItemRequerimiento> AgregarItemAsync(int idRequerimiento, ItemRequerimiento item,
        CancellationToken cancellationToken = default)
    {
        ValidarPertenencia(idRequerimiento, item);

        RequerimientoDetalleEntity fila = RequerimientoMapper.ToEntity(item);

        return EscribirItemAsync("compras.usp_RequerimientoItem_AgregarAlmacen",
            new
            {
                fila.IdRequerimiento,
                fila.IdMaterial,
                fila.Cantidad,
                fila.IdPresupuestoDetalle,
                fila.Observacion
            },
            cancellationToken);
    }

    public Task<ItemRequerimiento> ActualizarItemAsync(int idRequerimiento, ItemRequerimiento item,
        CamposItemRequerimiento campos, CancellationToken cancellationToken = default)
    {
        ValidarPertenencia(idRequerimiento, item);

        if (campos == CamposItemRequerimiento.Ninguno || (campos & ~CamposItemRequerimiento.Todos) != 0)
            throw new ValidacionComprasException("REQUERIMIENTO_PATCH_INVALIDO", "Indica campos editables.");

        RequerimientoDetalleEntity fila = RequerimientoMapper.ToEntity(item);

        return EscribirItemAsync("compras.usp_RequerimientoItem_ActualizarAlmacen",
            new
            {
                fila.IdRequerimiento,
                fila.IdRequerimientoDetalle,
                fila.Cantidad,
                fila.IdPresupuestoDetalle,
                fila.Observacion,
                CambiarCantidad = campos.HasFlag(CamposItemRequerimiento.Cantidad),
                CambiarPartida = campos.HasFlag(CamposItemRequerimiento.Partida),
                CambiarObservacion = campos.HasFlag(CamposItemRequerimiento.Observacion)
            }, cancellationToken);
    }

    public Task EliminarItemAsync(int idRequerimiento, int idDetalle, CancellationToken cancellationToken = default)
        => TraductorErroresSql.EjecutarAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using IDbConnection db = factory.CreateConnection();

            return await db.ExecuteAsync(new CommandDefinition("compras.usp_RequerimientoItem_EliminarAlmacen",
                new { IdRequerimiento = idRequerimiento, IdRequerimientoDetalle = idDetalle },
                commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        });

    private Task<ItemRequerimiento> EscribirItemAsync(string procedimiento, object parametros, CancellationToken ct)
        => TraductorErroresSql.EjecutarAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            using IDbConnection db = factory.CreateConnection();

            RequerimientoDetalleEntity fila = await db.QuerySingleAsync<RequerimientoDetalleEntity>(new CommandDefinition(procedimiento,
                parametros, commandType: CommandType.StoredProcedure, cancellationToken: ct));

            return RequerimientoMapper.ToDomain(fila);
        });

    private static void ValidarPertenencia(int idRequerimiento, ItemRequerimiento item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (idRequerimiento != item.IdRequerimiento)
            throw new ItemRequerimientoNoEncontradoException(idRequerimiento, item.IdRequerimientoDetalle);
    }
}
