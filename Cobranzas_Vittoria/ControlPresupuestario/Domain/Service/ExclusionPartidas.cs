using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Service;

/// <summary>Resuelve exclusiones jerárquicas sobre el catálogo, sin depender de montos ni de reportes.</summary>
public static class ExclusionPartidas
{
    public static IReadOnlySet<int> Resolver(IReadOnlyDictionary<int, CatalogoPartida> catalogo,
        IReadOnlyCollection<int>? partidasExcluidas)
    {
        HashSet<int> excluidas = partidasExcluidas?.ToHashSet() ?? new HashSet<int>();
        foreach (int id in excluidas)
        {
            if (id <= 0)
                throw ValidacionPresupuestariaException.IdentificadorInvalido("PartidasExcluidas");

            if (!catalogo.ContainsKey(id))
                throw new CatalogoPartidaNoEncontradoException(id);
        }

        if (excluidas.Count == 0)
            return excluidas;

        ILookup<int, CatalogoPartida> hijas = catalogo.Values
            .Where(p => p.IdPartidaPadre.HasValue)
            .ToLookup(p => p.IdPartidaPadre!.Value);
        Stack<int> pendientes = new(excluidas);

        while (pendientes.TryPop(out int idPadre))
        {
            foreach (CatalogoPartida hija in hijas[idPadre])
            {
                if (excluidas.Add(hija.IdCatalogoPartida))
                    pendientes.Push(hija.IdCatalogoPartida);
            }
        }

        return excluidas;
    }
}
