namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

/// <summary>Indica que no existe un resolvedor registrado para un tipo de destinatario.</summary>
/// <remarks>
/// Lanzar cuando una regla utiliza un tipo que el backend no puede interpretar.
/// No usar cuando el tipo sí está soportado pero su búsqueda no encuentra usuarios;
/// ese resultado debe evaluarse al validar los destinatarios de la notificación.
/// </remarks>
public sealed class TipoDestinatarioNoSoportadoException : ConfiguracionNotificacionException
{
    /// <summary>Código estable para un tipo de destinatario sin resolvedor.</summary>
    public const string CODIGO = "NOTIFICACION_TIPO_DESTINATARIO_NO_SOPORTADO";

    /// <summary>Inicializa el error para el tipo de destinatario no soportado.</summary>
    /// <param name="tipoDestinatario">Nombre del tipo configurado, por ejemplo, ROL o USUARIO.</param>
    public TipoDestinatarioNoSoportadoException(string tipoDestinatario)
        : base(CODIGO, $"No existe un resolvedor registrado para el tipo de destinatario {tipoDestinatario}.")
    {
    }
}
