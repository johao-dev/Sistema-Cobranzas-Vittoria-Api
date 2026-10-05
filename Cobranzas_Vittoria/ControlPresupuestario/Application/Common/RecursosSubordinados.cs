using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

/// <summary>
/// Versiones y partidas de versión son recursos subordinados del presupuesto en HTTP: una ruta
/// /presupuestos/{id}/versiones/{versionId} solo es válida si la versión pertenece a ese presupuesto.
/// </summary>
internal static class RecursosSubordinados
{
    public static async Task<Domain.Model.PresupuestoVersion> ObtenerDelPresupuestoAsync(this IPresupuestoVersionRepository versiones,
        int idPresupuesto, int idPresupuestoVersion)
    {
        var version = await versiones.ObtenerAsync(idPresupuestoVersion);
        return version is not null && version.PerteneceA(idPresupuesto)
            ? version
            : throw new VersionPresupuestoNoEncontradaException(idPresupuestoVersion);
    }

    public static async Task<Domain.Model.PresupuestoDetalle> ObtenerDeLaVersionAsync(this IPresupuestoDetalleRepository detalles,
        int idPresupuestoVersion, int idPresupuestoDetalle)
        => (await detalles.ListarPorVersionAsync(idPresupuestoVersion))
            .FirstOrDefault(d => d.IdPresupuestoDetalle == idPresupuestoDetalle)
            ?? throw new PresupuestoDetalleNoEncontradoException(idPresupuestoDetalle);
}
