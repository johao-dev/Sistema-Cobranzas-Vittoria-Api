using System.Globalization;
using Cobranzas_Vittoria.Notificaciones.Domain.Validator;
using static Cobranzas_Vittoria.Notificaciones.Domain.Model.NotificacionDestinatario;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Model;

/// <summary>Instrucción inmutable para resolver destinatarios al generar una notificación.</summary>
public class ReglaDestinatario
{
    public int IdReglaDestinatario { get; private set; }
    public int IdReglaNotificacion { get; private set; }
    public int IdTipoDestinatario { get; private set; }
    public TipoDestinatario TipoDestinatario { get; private set; }
    public string? Valor { get; private set; }
    public TipoEnvioConfig TipoEnvio { get; private set; }

    private ReglaDestinatario() { }

    /// <summary>Crea una instrucción sin identidad ni pertenencia persistida; el agregado administra su incorporación.</summary>
    /// <remarks>El ID de catálogo y la estrategia se obtienen del mismo registro; no se asume una numeración fija.</remarks>
    public static ReglaDestinatario Crear(
        int idTipoDestinatario, TipoDestinatario tipoDestinatario, string? valor, TipoEnvioConfig tipoEnvio)
    {
        string? valorNormalizado = ReglaDestinatarioValidator.ValidarYNormalizarValor(
            idTipoDestinatario, tipoDestinatario, valor, tipoEnvio);

        return new ReglaDestinatario
        {
            IdTipoDestinatario = idTipoDestinatario,
            TipoDestinatario = tipoDestinatario,
            Valor = valorNormalizado,
            TipoEnvio = tipoEnvio
        };
    }

    /// <summary>Compara la estrategia, el valor y la participación, independientemente de los IDs de las filas.</summary>
    internal bool TieneMismaConfiguracion(ReglaDestinatario otro)
    {
        if (TipoDestinatario != otro.TipoDestinatario || TipoEnvio != otro.TipoEnvio)
        {
            return false;
        }

        // La reconstrucción conserva el texto histórico; la igualdad de referencias fijas es numérica.
        if (TipoDestinatario is TipoDestinatario.Rol or TipoDestinatario.Usuario
            && int.TryParse(Valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int identificador)
            && int.TryParse(otro.Valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int otroIdentificador))
        {
            return identificador == otroIdentificador;
        }

        return string.Equals(Valor, otro.Valor, StringComparison.Ordinal);
    }

    /// <summary>Restaura una instrucción persistida sin normalizar ni validar como un alta nueva.</summary>
    public static ReglaDestinatario Reconstruir(
        int idReglaDestinatario, int idReglaNotificacion, int idTipoDestinatario,
        TipoDestinatario tipoDestinatario, string? valor, TipoEnvioConfig tipoEnvio)
    {
        return new ReglaDestinatario
        {
            IdReglaDestinatario = idReglaDestinatario,
            IdReglaNotificacion = idReglaNotificacion,
            IdTipoDestinatario = idTipoDestinatario,
            TipoDestinatario = tipoDestinatario,
            Valor = valor,
            TipoEnvio = tipoEnvio
        };
    }
}
