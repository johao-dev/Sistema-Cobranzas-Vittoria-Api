namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

/// <summary>Indica que no se suministró una variable requerida para renderizar una plantilla.</summary>
/// <remarks>
/// Lanzar antes de registrar la notificación cuando el asunto o cuerpo contiene una
/// variable para la que no se proporcionó un valor. Identificar únicamente el evento
/// y el nombre de la variable, sin incorporar el contenido del correo al error.
/// </remarks>
public sealed class VariablePlantillaFaltanteException : ConfiguracionNotificacionException
{
    /// <summary>Código estable para una variable requerida que no fue suministrada.</summary>
    public const string CODIGO = "NOTIFICACION_VARIABLE_PLANTILLA_FALTANTE";

    /// <summary>Inicializa el error indicando el evento y la variable ausente.</summary>
    /// <param name="codigoEvento">Código funcional del evento cuya plantilla se renderiza.</param>
    /// <param name="nombreVariable">Nombre de la variable requerida, sin su valor.</param>
    public VariablePlantillaFaltanteException(string codigoEvento, string nombreVariable)
        : base(CODIGO, $"No se proporcionó la variable {nombreVariable} requerida por la plantilla del evento {codigoEvento}.")
    {
    }
}
