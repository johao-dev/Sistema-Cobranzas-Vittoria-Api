namespace Cobranzas_Vittoria.Compras.Contracts.Eventos;

/// <summary>
/// Ocurrencia posterior al commit; IdRequerimiento permite consultar sus referencias.
/// FechaOcurrenciaUtc es el instante observado por la aplicación, no la auditoría SQL local.
/// </summary>
public sealed record RequerimientoCreadoEvent(
    Guid IdEvento,
    int IdRequerimiento,
    string NumeroRequerimiento,
    DateOnly FechaRequerimiento,
    DateTime FechaOcurrenciaUtc);
