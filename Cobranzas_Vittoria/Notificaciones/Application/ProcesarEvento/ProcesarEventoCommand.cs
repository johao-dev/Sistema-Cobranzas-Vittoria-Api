namespace Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;

/// <summary>Datos de una ocurrencia; el emisor conserva IdEvento al volver a entregar el mismo evento.</summary>
/// <param name="IdEvento">Identidad de la ocurrencia, independiente del registro de negocio.</param>
/// <param name="CodigoEvento">Código funcional del catálogo.</param>
/// <param name="IdReferencia">ID del registro de negocio.</param>
/// <param name="Variables">Valores sin codificar, formateados por el emisor con la cultura deseada.</param>
public sealed record ProcesarEventoCommand(
    Guid IdEvento,
    string CodigoEvento,
    int IdReferencia,
    IReadOnlyDictionary<string, string?> Variables);
