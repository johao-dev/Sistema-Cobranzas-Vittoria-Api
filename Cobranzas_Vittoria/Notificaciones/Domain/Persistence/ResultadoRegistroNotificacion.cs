namespace Cobranzas_Vittoria.Notificaciones.Domain.Persistence;

/// <summary>Resultado de un registro idempotente: identifica tanto una inserción como una ocurrencia ya registrada.</summary>
/// <param name="IdNotificacion">Identificador de la notificación nueva o existente.</param>
/// <param name="Creada">True si se insertó; false si el mismo IdEvento ya estaba registrado.</param>
public sealed record ResultadoRegistroNotificacion(long IdNotificacion, bool Creada);
