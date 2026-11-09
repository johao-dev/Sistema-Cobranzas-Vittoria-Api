using System.Globalization;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using static Cobranzas_Vittoria.Notificaciones.Domain.Model.NotificacionDestinatario;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Validator;

/// <summary>Valida la combinación de estrategia, valor y participación de un destinatario configurado.</summary>
internal static class ReglaDestinatarioValidator
{
    private const int LONGITUD_MAXIMA_VALOR = 150;

    internal static string? ValidarYNormalizarValor(
        int idTipoDestinatario, TipoDestinatario tipoDestinatario, string? valor, TipoEnvioConfig tipoEnvio)
    {
        if (idTipoDestinatario <= 0 || !Enum.IsDefined(tipoDestinatario))
        {
            throw new ValidacionNegocioNotificacionException(
                "REGLA_DESTINATARIO_TIPO_INVALIDO", "El tipo de destinatario debe ser válido y tener un identificador positivo.");
        }

        if (!Enum.IsDefined(tipoEnvio))
        {
            throw new ValidacionNegocioNotificacionException(
                "REGLA_DESTINATARIO_ENVIO_INVALIDO", "El tipo de envío debe ser TO, CC o BCC.");
        }

        string? valorNormalizado = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

        if (valorNormalizado is not null && valorNormalizado.Length > LONGITUD_MAXIMA_VALOR)
        {
            throw new ValidacionNegocioNotificacionException(
                "REGLA_DESTINATARIO_VALOR_INVALIDO", $"El valor no debe superar los {LONGITUD_MAXIMA_VALOR} caracteres.");
        }

        if (tipoDestinatario is TipoDestinatario.Rol or TipoDestinatario.Usuario)
        {
            if (!int.TryParse(valorNormalizado, NumberStyles.None, CultureInfo.InvariantCulture, out int identificador) || identificador <= 0)
            {
                throw new ValidacionNegocioNotificacionException(
                    "REGLA_DESTINATARIO_IDENTIFICADOR_INVALIDO", "El destinatario fijo requiere un identificador positivo.");
            }

            // Una misma referencia debe compararse igual aunque llegue con ceros iniciales.
            return identificador.ToString(CultureInfo.InvariantCulture);
        }

        if (valorNormalizado is not null)
        {
            throw new ValidacionNegocioNotificacionException(
                "REGLA_DESTINATARIO_VALOR_NO_PERMITIDO", "El destinatario dinámico se resuelve desde el evento y no admite un valor fijo.");
        }

        return null;
    }
}
