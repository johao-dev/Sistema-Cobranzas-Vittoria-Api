namespace Cobranzas_Vittoria.Notificaciones.Application.RequerimientoCreado;

public interface IConsultaRequerimientoCreado
{
    Task<DatosRequerimientoCreado> ObtenerAsync(int idRequerimiento, CancellationToken cancellationToken = default);
}

public sealed record DatosRequerimientoCreado(string Proyecto, string Solicitante);
