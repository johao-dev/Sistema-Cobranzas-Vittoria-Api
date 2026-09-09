namespace Cobranzas_Vittoria.Interfaces
{
    public interface IRequerimientoService
    {
        Task<IEnumerable<Cobranzas_Vittoria.Entities.Requerimiento>> ListAsync(string? estado, int? idEspecialidad, int? idProyecto);
        Task<object?> GetAsync(int idRequerimiento);
        Task<int> CrearAsync(Cobranzas_Vittoria.Dtos.Compras.RequerimientoCreateDto dto);
        Task UpdateAsync(int idRequerimiento, int idUsuarioActual, Cobranzas_Vittoria.Dtos.Compras.RequerimientoUpdateDto dto);
        Task<bool> PuedeEditarAsync(int idRequerimiento);
        Task EnviarAsync(int idRequerimiento, int idUsuarioActual, string? observacion);
        Task ProcesarStockAsync(int idRequerimiento, int idUsuarioActual, string resultado, string? observacion);
        Task AprobarAsync(int idRequerimiento, int idUsuarioActual, string? observacion);
        Task RechazarAsync(int idRequerimiento, int idUsuarioActual, string? observacion);
        Task EnviarComprasAsync(int idRequerimiento, int idUsuarioActual, string? observacion);
    }
}
