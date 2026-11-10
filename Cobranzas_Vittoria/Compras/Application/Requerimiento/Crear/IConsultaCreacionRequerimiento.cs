namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;

public interface IConsultaCreacionRequerimiento
{
    Task<ContextoCreacionRequerimiento> ObtenerContextoAsync(int idProyecto, int idSolicitante,
        IReadOnlyCollection<int> materiales, IReadOnlyCollection<int> partidas, CancellationToken ct);

    Task<DisponibilidadNumeroRequerimiento> ObtenerNumeracionAsync(string numeroSolicitado, CancellationToken ct);
}
