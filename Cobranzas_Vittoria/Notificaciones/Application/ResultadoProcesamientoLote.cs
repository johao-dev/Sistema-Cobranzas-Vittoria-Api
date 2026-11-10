namespace Cobranzas_Vittoria.Notificaciones.Application;

/// <summary>Un conflicto no cuenta como guardado: otro procesamiento pudo haber cambiado la fila.</summary>
public sealed record ResultadoProcesamientoLote(int Evaluadas, int Guardadas, int ConflictosConcurrencia);
