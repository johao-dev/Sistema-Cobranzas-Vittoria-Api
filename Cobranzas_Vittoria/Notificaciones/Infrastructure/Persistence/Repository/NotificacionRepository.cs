using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Persistence.Entity;
using Dapper;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.Persistence.Repository;

public sealed class NotificacionRepository(IDbConnectionFactory factory) : INotificacionRepository
{
    private const string LEER_LOTE = """
        SELECT n.* FROM Notificaciones.Notificacion n
        INNER JOIN @Ids i ON i.Id = n.IdNotificacion ORDER BY n.IdNotificacion;
        SELECT d.* FROM Notificaciones.NotificacionDestinatario d
        INNER JOIN @Ids i ON i.Id = d.IdNotificacion ORDER BY d.IdNotificacionDestinatario;
        """;

    public async Task<ResultadoRegistroNotificacion> RegistrarSiNoExisteAsync(
        Notificacion notificacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificacion);
        cancellationToken.ThrowIfCancellationRequested();

        if (notificacion.IdNotificacion != 0 || notificacion.Estado != Notificacion.EstadoNotificacion.Pendiente
            || notificacion.Intentos != 0 || notificacion.FechaCreacion.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Solo se registran notificaciones nuevas pendientes.", nameof(notificacion));
        }

        using IDbConnection db = factory.CreateConnection();
        using IDbTransaction tx = db.BeginTransaction(IsolationLevel.Serializable);

        // El bloqueo de rango del índice único serializa también dos altas de la misma ocurrencia.
        long? existente = await db.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("""
            SELECT IdNotificacion FROM Notificaciones.Notificacion WITH (UPDLOCK, HOLDLOCK)
            WHERE IdEvento = @IdEvento;
            """, new { notificacion.IdEvento }, tx, cancellationToken: cancellationToken));

        if (existente.HasValue)
        {
            tx.Commit();
            return new ResultadoRegistroNotificacion(existente.Value, false);
        }

        long id = await db.QuerySingleAsync<long>(new CommandDefinition("""
            INSERT INTO Notificaciones.Notificacion
                (IdEvento, IdCodigoEventoNotificacion, IdReferencia, AsuntoGenerado, CuerpoHtmlGenerado, FechaCreacion)
            OUTPUT INSERTED.IdNotificacion
            VALUES (@IdEvento, @IdCodigoEventoNotificacion, @IdReferencia, @AsuntoGenerado, @CuerpoHtmlGenerado, @FechaCreacion);
            """, new { notificacion.IdEvento, notificacion.IdCodigoEventoNotificacion, notificacion.IdReferencia,
                notificacion.AsuntoGenerado, notificacion.CuerpoHtmlGenerado, notificacion.FechaCreacion },
            tx, cancellationToken: cancellationToken));

        foreach (NotificacionDestinatario destinatario in notificacion.Destinatarios)
        {
            await db.ExecuteAsync(new CommandDefinition("""
                INSERT INTO Notificaciones.NotificacionDestinatario (IdNotificacion, Correo, Nombre, TipoEnvio)
                VALUES (@Id, @Correo, @Nombre, @TipoEnvio);
                """, new { Id = id, destinatario.Correo, destinatario.Nombre,
                    TipoEnvio = ConversionSql.EscribirEnvio(destinatario.TipoEnvio) }, tx,
                cancellationToken: cancellationToken));
        }

        tx.Commit();
        return new ResultadoRegistroNotificacion(id, true);
    }

    public async Task<Notificacion?> ObtenerPorIdAsync(long idNotificacion, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Notificacion> lote = await LeerAsync("""
            DECLARE @Ids TABLE (Id BIGINT PRIMARY KEY);
            INSERT INTO @Ids SELECT IdNotificacion FROM Notificaciones.Notificacion WHERE IdNotificacion = @Id;
            """, new { Id = idNotificacion }, IsolationLevel.Serializable, cancellationToken);

        return lote.SingleOrDefault();
    }

    public Task<IReadOnlyCollection<Notificacion>> ReclamarPendientesAsync(
        int cantidad, DateTime ahoraUtc, CancellationToken cancellationToken = default)
    {
        ValidarLote(cantidad, ahoraUtc);

        // READCOMMITTEDLOCK permite READPAST con RCSI ON u OFF. No se combina con ROWLOCK
        // (mismo grupo de hints). READPAST no promete omitir bloqueos de página.
        return LeerAsync("""
            DECLARE @Ids TABLE (Id BIGINT PRIMARY KEY);
            ;WITH Pendientes AS (
                SELECT TOP (@Cantidad) * FROM Notificaciones.Notificacion
                    WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
                WHERE Estado = N'PENDIENTE' AND (FechaProximoIntento IS NULL OR FechaProximoIntento <= @AhoraUtc)
                ORDER BY FechaCreacion, IdNotificacion
            )
            UPDATE Pendientes SET Estado = N'PROCESANDO', Intentos = Intentos + 1,
                FechaInicioProcesamiento = @AhoraUtc, FechaProximoIntento = NULL
            OUTPUT INSERTED.IdNotificacion INTO @Ids;
            """, new { Cantidad = cantidad, AhoraUtc = ahoraUtc }, IsolationLevel.ReadCommitted, cancellationToken);
    }

    public Task<IReadOnlyCollection<Notificacion>> ObtenerProcesamientosVencidosAsync(
        int cantidad, DateTime fechaLimiteUtc, CancellationToken cancellationToken = default)
    {
        ValidarLote(cantidad, fechaLimiteUtc);

        return LeerAsync("""
            DECLARE @Ids TABLE (Id BIGINT PRIMARY KEY);
            INSERT INTO @Ids SELECT TOP (@Cantidad) IdNotificacion FROM Notificaciones.Notificacion
            WHERE Estado = N'PROCESANDO' AND FechaInicioProcesamiento < @FechaLimiteUtc
            ORDER BY FechaInicioProcesamiento, IdNotificacion;
            """, new { Cantidad = cantidad, FechaLimiteUtc = fechaLimiteUtc }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<bool> GuardarResultadoAsync(Notificacion notificacion, int intentoEsperado,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificacion);

        if (intentoEsperado <= 0 || notificacion.Intentos != intentoEsperado)
        {
            throw new ArgumentException("La entidad debe corresponder al intento observado.", nameof(intentoEsperado));
        }

        string estado = notificacion.Estado switch
        {
            Notificacion.EstadoNotificacion.Pendiente => "PENDIENTE",
            Notificacion.EstadoNotificacion.Enviada => "ENVIADA",
            Notificacion.EstadoNotificacion.Error => "ERROR",
            _ => throw new ArgumentException("Estado de resultado no permitido.", nameof(notificacion))
        };

        cancellationToken.ThrowIfCancellationRequested();
        using IDbConnection db = factory.CreateConnection();

        int afectadas = await db.ExecuteAsync(new CommandDefinition("""
            UPDATE Notificaciones.Notificacion SET Estado = @Estado,
                FechaInicioProcesamiento = @FechaInicioProcesamiento, FechaProximoIntento = @FechaProximoIntento,
                FechaEnvio = @FechaEnvio, UltimoError = @UltimoError
            WHERE IdNotificacion = @IdNotificacion AND Estado = N'PROCESANDO' AND Intentos = @IntentoEsperado;
            """, new { notificacion.IdNotificacion, Estado = estado, IntentoEsperado = intentoEsperado,
                notificacion.FechaInicioProcesamiento, notificacion.FechaProximoIntento, notificacion.FechaEnvio,
                notificacion.UltimoError }, cancellationToken: cancellationToken));

        return afectadas == 1;
    }

    private async Task<IReadOnlyCollection<Notificacion>> LeerAsync(string seleccion, object parametros,
        IsolationLevel aislamiento, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using IDbConnection db = factory.CreateConnection();
        using IDbTransaction tx = db.BeginTransaction(aislamiento);

        using SqlMapper.GridReader resultados = await db.QueryMultipleAsync(
            new CommandDefinition(seleccion + LEER_LOTE, parametros, tx, cancellationToken: cancellationToken));
        List<NotificacionRegistro> cabeceras = (await resultados.ReadAsync<NotificacionRegistro>()).ToList();
        ILookup<long, DestinatarioRegistro> destinatarios =
            (await resultados.ReadAsync<DestinatarioRegistro>()).ToLookup(d => d.IdNotificacion);
        List<Notificacion> lote = cabeceras.Select(n => n.Reconstruir(destinatarios[n.IdNotificacion])).ToList();

        tx.Commit();
        return lote.AsReadOnly();
    }

    private static void ValidarLote(int cantidad, DateTime fechaUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidad);

        if (fechaUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Se requiere una fecha UTC.", nameof(fechaUtc));
        }
    }
}
