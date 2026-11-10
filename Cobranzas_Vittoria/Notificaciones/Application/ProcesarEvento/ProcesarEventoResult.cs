namespace Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;

public enum EstadoProcesarEvento
{
    Creada,
    YaRegistrada,
    Omitida
}

public enum MotivoOmisionEvento
{
    EventoSinSuscripcion,
    ReglaDesactivada,
    PlantillaDesactivada
}

/// <summary>El ID identifica el registro nuevo o existente; solo las omisiones tienen motivo e ID nulo.</summary>
public sealed record ProcesarEventoResult(
    EstadoProcesarEvento Estado,
    long? IdNotificacion,
    MotivoOmisionEvento? MotivoOmision = null);
