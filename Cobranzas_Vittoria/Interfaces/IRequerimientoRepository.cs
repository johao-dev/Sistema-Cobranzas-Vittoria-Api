namespace Cobranzas_Vittoria.Interfaces
{
    public interface IRequerimientoRepository
    {
        Task<IEnumerable<Cobranzas_Vittoria.Entities.Requerimiento>> ListAsync(string? estado, int? idEspecialidad, int? idProyecto);
        Task<(Cobranzas_Vittoria.Entities.Requerimiento? head, List<Cobranzas_Vittoria.Entities.RequerimientoDetalle> items, List<Cobranzas_Vittoria.Entities.RequerimientoValidacion> validaciones)> GetAsync(int idRequerimiento);
        Task<int> CrearAsync(Cobranzas_Vittoria.Dtos.Compras.RequerimientoCreateDto dto);
        Task UpdateAsync(int idRequerimiento, Cobranzas_Vittoria.Dtos.Compras.RequerimientoUpdateDto dto);
        Task<bool> PuedeEditarAsync(int idRequerimiento);
        Task<bool> EsSolicitanteAsync(int idRequerimiento, int idUsuario);
        Task EnviarAsync(int idRequerimiento, int idUsuario, string? observacion);
        Task ProcesarStockAsync(int idRequerimiento, int idUsuario, string resultado, string? observacion);
        Task AprobarAsync(int idRequerimiento, int idUsuario, string? observacion);
        Task RechazarAsync(int idRequerimiento, int idUsuario, string? observacion);
        Task EnviarComprasAsync(int idRequerimiento, int idUsuario, string? observacion);
    }
}
