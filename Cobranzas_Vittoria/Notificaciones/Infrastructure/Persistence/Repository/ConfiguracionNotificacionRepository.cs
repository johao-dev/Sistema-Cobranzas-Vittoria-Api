using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Persistence.Entity;
using Dapper;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.Persistence.Repository;

public sealed class ConfiguracionNotificacionRepository(IDbConnectionFactory factory) : IConfiguracionNotificacionRepository
{
    public async Task<ConfiguracionNotificacion?> ObtenerPorCodigoEventoAsync(
        string codigoEvento, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using IDbConnection db = factory.CreateConnection();
        using IDbTransaction tx = db.BeginTransaction(IsolationLevel.Serializable);

        using SqlMapper.GridReader datos = await db.QueryMultipleAsync(new CommandDefinition("""
            SELECT IdCodigoEvento FROM Notificaciones.CodigoEventoNotificacion WHERE Codigo = @CodigoEvento;
            SELECT r.* FROM Notificaciones.ReglaNotificacion r
            JOIN Notificaciones.CodigoEventoNotificacion e ON e.IdCodigoEvento = r.IdCodigoEventoNotificacion
            WHERE e.Codigo = @CodigoEvento;
            SELECT d.*, t.Nombre AS Tipo FROM Notificaciones.ReglaDestinatario d
            JOIN Notificaciones.TipoDestinatario t ON t.IdTipoDestinatario = d.IdTipoDestinatario
            JOIN Notificaciones.ReglaNotificacion r ON r.IdReglaNotificacion = d.IdReglaNotificacion
            JOIN Notificaciones.CodigoEventoNotificacion e ON e.IdCodigoEvento = r.IdCodigoEventoNotificacion
            WHERE e.Codigo = @CodigoEvento ORDER BY d.IdReglaDestinatario;
            SELECT p.* FROM Notificaciones.PlantillaCorreo p
            JOIN Notificaciones.CodigoEventoNotificacion e ON e.IdCodigoEvento = p.IdCodigoEventoNotificacion
            WHERE e.Codigo = @CodigoEvento;
            """, new { CodigoEvento = codigoEvento }, tx, cancellationToken: cancellationToken));

        int? id = await datos.ReadSingleOrDefaultAsync<int?>();
        ReglaRegistro? regla = await datos.ReadSingleOrDefaultAsync<ReglaRegistro>();
        List<InstruccionRegistro> instrucciones = (await datos.ReadAsync<InstruccionRegistro>()).ToList();
        PlantillaRegistro? plantilla = await datos.ReadSingleOrDefaultAsync<PlantillaRegistro>();

        ConfiguracionNotificacion? configuracion = id.HasValue
            ? new ConfiguracionNotificacion(id.Value, codigoEvento,
                regla is null ? null : ReglaNotificacion.Reconstruir(regla.IdReglaNotificacion,
                    regla.IdCodigoEventoNotificacion, regla.Nombre, regla.Activo,
                    instrucciones.Select(d => ReglaDestinatario.Reconstruir(d.IdReglaDestinatario,
                        d.IdReglaNotificacion, d.IdTipoDestinatario, LeerTipo(d.Tipo), d.Valor,
                        ConversionSql.LeerEnvio(d.TipoEnvio)))),
                plantilla is null ? null : PlantillaCorreo.Reconstruir(plantilla.IdPlantilla,
                    plantilla.IdCodigoEventoNotificacion, plantilla.Asunto, plantilla.CuerpoHtml, plantilla.Activo))
            : null;

        tx.Commit();
        return configuracion;
    }

    public async Task GuardarReglaAsync(ReglaNotificacion regla, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(regla);
        cancellationToken.ThrowIfCancellationRequested();
        using IDbConnection db = factory.CreateConnection();
        using IDbTransaction tx = db.BeginTransaction(IsolationLevel.Serializable);

        int? existente = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("""
            SELECT IdReglaNotificacion FROM Notificaciones.ReglaNotificacion WITH (UPDLOCK, HOLDLOCK)
            WHERE IdCodigoEventoNotificacion = @IdCodigoEventoNotificacion;
            """, new { regla.IdCodigoEventoNotificacion }, tx, cancellationToken: cancellationToken));

        ValidarIdentidad(regla.IdReglaNotificacion, existente);

        int id = existente ?? await db.QuerySingleAsync<int>(new CommandDefinition("""
            INSERT INTO Notificaciones.ReglaNotificacion (IdCodigoEventoNotificacion, Nombre, Activo)
            OUTPUT INSERTED.IdReglaNotificacion VALUES (@IdCodigoEventoNotificacion, @Nombre, @Activo);
            """, new { regla.IdCodigoEventoNotificacion, regla.Nombre, regla.Activo }, tx,
            cancellationToken: cancellationToken));

        if (existente.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition("""
                UPDATE Notificaciones.ReglaNotificacion SET Nombre = @Nombre, Activo = @Activo
                WHERE IdReglaNotificacion = @Id;
                """, new { Id = id, regla.Nombre, regla.Activo }, tx, cancellationToken: cancellationToken));
        }

        List<int> conservadas = new();

        foreach (ReglaDestinatario instruccion in regla.Destinatarios)
        {
            if (instruccion.IdReglaNotificacion != 0 && instruccion.IdReglaNotificacion != id)
            {
                throw new InvalidOperationException("La instrucción pertenece a otra regla.");
            }

            string? tipoCatalogo = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
                SELECT Nombre FROM Notificaciones.TipoDestinatario WHERE IdTipoDestinatario = @IdTipoDestinatario;
                """, new { instruccion.IdTipoDestinatario }, tx, cancellationToken: cancellationToken));

            if (tipoCatalogo is null || LeerTipo(tipoCatalogo) != instruccion.TipoDestinatario)
            {
                throw new InvalidOperationException("El tipo de destinatario no coincide con el catálogo.");
            }

            if (instruccion.IdReglaDestinatario > 0)
            {
                int afectadas = await db.ExecuteAsync(new CommandDefinition("""
                    UPDATE Notificaciones.ReglaDestinatario SET IdTipoDestinatario = @IdTipoDestinatario,
                        Valor = @Valor, TipoEnvio = @TipoEnvio
                    WHERE IdReglaDestinatario = @IdReglaDestinatario AND IdReglaNotificacion = @Id;
                    """, new { Id = id, instruccion.IdReglaDestinatario, instruccion.IdTipoDestinatario,
                        instruccion.Valor, TipoEnvio = ConversionSql.EscribirEnvio(instruccion.TipoEnvio) }, tx,
                    cancellationToken: cancellationToken));

                if (afectadas != 1)
                {
                    throw new InvalidOperationException("La instrucción persistida no pertenece a esta regla.");
                }

                conservadas.Add(instruccion.IdReglaDestinatario);
            }
            else
            {
                int nueva = await db.QuerySingleAsync<int>(new CommandDefinition("""
                    INSERT INTO Notificaciones.ReglaDestinatario (IdReglaNotificacion, IdTipoDestinatario, Valor, TipoEnvio)
                    OUTPUT INSERTED.IdReglaDestinatario VALUES (@Id, @IdTipoDestinatario, @Valor, @TipoEnvio);
                    """, new { Id = id, instruccion.IdTipoDestinatario, instruccion.Valor,
                        TipoEnvio = ConversionSql.EscribirEnvio(instruccion.TipoEnvio) }, tx,
                    cancellationToken: cancellationToken));
                conservadas.Add(nueva);
            }
        }

        await db.ExecuteAsync(new CommandDefinition("""
            DELETE FROM Notificaciones.ReglaDestinatario
            WHERE IdReglaNotificacion = @Id AND IdReglaDestinatario NOT IN @Conservadas;
            """, new { Id = id, Conservadas = conservadas }, tx, cancellationToken: cancellationToken));

        tx.Commit();
    }

    public async Task GuardarPlantillaAsync(PlantillaCorreo plantilla, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plantilla);
        cancellationToken.ThrowIfCancellationRequested();
        using IDbConnection db = factory.CreateConnection();
        using IDbTransaction tx = db.BeginTransaction(IsolationLevel.Serializable);

        int? existente = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("""
            SELECT IdPlantilla FROM Notificaciones.PlantillaCorreo WITH (UPDLOCK, HOLDLOCK)
            WHERE IdCodigoEventoNotificacion = @IdCodigoEventoNotificacion;
            """, new { plantilla.IdCodigoEventoNotificacion }, tx, cancellationToken: cancellationToken));

        ValidarIdentidad(plantilla.IdPlantilla, existente);

        string sql = existente.HasValue ? """
            UPDATE Notificaciones.PlantillaCorreo SET Asunto = @Asunto, CuerpoHtml = @CuerpoHtml, Activo = @Activo
            WHERE IdCodigoEventoNotificacion = @IdCodigoEventoNotificacion;
            """ : """
            INSERT INTO Notificaciones.PlantillaCorreo (IdCodigoEventoNotificacion, Asunto, CuerpoHtml, Activo)
            VALUES (@IdCodigoEventoNotificacion, @Asunto, @CuerpoHtml, @Activo);
            """;

        await db.ExecuteAsync(new CommandDefinition(sql, new { plantilla.IdCodigoEventoNotificacion,
            plantilla.Asunto, plantilla.CuerpoHtml, plantilla.Activo }, tx, cancellationToken: cancellationToken));

        tx.Commit();
    }

    private static void ValidarIdentidad(int recibida, int? existente)
    {
        if (recibida != 0 && recibida != existente)
        {
            throw new InvalidOperationException("La identidad no corresponde a la configuración del evento.");
        }
    }

    private static TipoDestinatario LeerTipo(string nombre) => nombre switch
    {
        "ROL" => TipoDestinatario.Rol,
        "USUARIO" => TipoDestinatario.Usuario,
        _ => throw new TipoDestinatarioNoSoportadoException(nombre)
    };

    private sealed class ReglaRegistro
    {
        public int IdReglaNotificacion { get; set; }
        public int IdCodigoEventoNotificacion { get; set; }
        public string Nombre { get; set; } = "";
        public bool Activo { get; set; }
    }

    private sealed class InstruccionRegistro
    {
        public int IdReglaDestinatario { get; set; }
        public int IdReglaNotificacion { get; set; }
        public int IdTipoDestinatario { get; set; }
        public string Tipo { get; set; } = "";
        public string? Valor { get; set; }
        public string TipoEnvio { get; set; } = "";
    }

    private sealed class PlantillaRegistro
    {
        public int IdPlantilla { get; set; }
        public int IdCodigoEventoNotificacion { get; set; }
        public string Asunto { get; set; } = "";
        public string CuerpoHtml { get; set; } = "";
        public bool Activo { get; set; }
    }
}
