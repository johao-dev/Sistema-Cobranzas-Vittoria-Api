using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;

namespace Cobranzas_Vittoria.Notificaciones.Application.Services;

/// <summary>Selecciona una estrategia por tipo, independientemente del orden de registro.</summary>
public sealed class DestinatarioResolver
{
    private readonly IReadOnlyDictionary<TipoDestinatario, IDestinatarioResolver> _estrategias;

    public DestinatarioResolver(IEnumerable<IDestinatarioResolver> estrategias)
    {
        Dictionary<TipoDestinatario, IDestinatarioResolver> registro = new();

        foreach (IDestinatarioResolver estrategia in estrategias)
        {
            if (!registro.TryAdd(estrategia.Tipo, estrategia))
            {
                throw new InvalidOperationException($"Existe más de una estrategia para {estrategia.Tipo}.");
            }
        }

        _estrategias = registro;
    }

    public Task<IReadOnlyCollection<DireccionCorreo>> ResolverAsync(
        ReglaDestinatario instruccion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_estrategias.TryGetValue(instruccion.TipoDestinatario, out IDestinatarioResolver? estrategia))
        {
            throw new TipoDestinatarioNoSoportadoException(instruccion.TipoDestinatario.ToString());
        }

        return estrategia.ResolverAsync(instruccion, cancellationToken);
    }
}
