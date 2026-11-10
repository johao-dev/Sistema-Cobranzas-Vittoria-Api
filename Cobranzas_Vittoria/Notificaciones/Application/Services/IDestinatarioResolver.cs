using Cobranzas_Vittoria.Notificaciones.Domain.Model;

namespace Cobranzas_Vittoria.Notificaciones.Application.Services;

/// <summary>Resuelve una instrucción ROL o USUARIO sin limitarla por proyectos o compradores.</summary>
public interface IDestinatarioResolver
{
    TipoDestinatario Tipo { get; }

    /// <summary>Devuelve cero o más direcciones; omite usuarios sin correo y conserva todos los usuarios del rol.</summary>
    /// <remarks>
    /// No asigna TipoEnvio: lo determina la instrucción. Application valida con la fábrica del dominio
    /// y elimina duplicados por dirección y participación. Una estrategia sin soporte debe lanzar
    /// TipoDestinatarioNoSoportadoException; no se representa mediante una colección vacía.
    /// </remarks>
    Task<IReadOnlyCollection<DireccionCorreo>> ResolverAsync(
        ReglaDestinatario instruccion,
        CancellationToken cancellationToken = default);
}
