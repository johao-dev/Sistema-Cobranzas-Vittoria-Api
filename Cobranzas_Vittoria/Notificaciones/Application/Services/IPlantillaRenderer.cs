using Cobranzas_Vittoria.Notificaciones.Domain.Model;

namespace Cobranzas_Vittoria.Notificaciones.Application.Services;

/// <summary>Renderiza asunto y HTML sin persistir ni enviar el mensaje.</summary>
public interface IPlantillaRenderer
{
    /// <summary>Resuelve los marcadores de la plantilla utilizando valores sin codificar.</summary>
    /// <remarks>
    /// Una variable requerida ausente o nula produce VariablePlantillaFaltanteException antes del registro.
    /// El adaptador debe codificar valores según el contexto HTML (texto o atributo entre comillas),
    /// validar las URL de href permitiendo únicamente http/https y rechazar contextos no soportados.
    /// Codificar HTML por sí solo no neutraliza esquemas como javascript:. No admite HTML crudo en
    /// variables ni saltos CR/LF en el asunto. Los errores no deben incluir valores ni contenido del correo.
    /// Las claves de las variables son sensibles a mayúsculas y no se limita el contrato al evento inicial.
    /// </remarks>
    Task<ContenidoCorreoRenderizado> RenderizarAsync(
        string codigoEvento,
        PlantillaCorreo plantilla,
        IReadOnlyDictionary<string, string?> variables,
        CancellationToken cancellationToken = default);
}
