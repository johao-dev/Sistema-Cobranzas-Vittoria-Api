namespace Cobranzas_Vittoria.Interfaces
{
    public interface IRequerimientoService
    {
        Task<IEnumerable<Cobranzas_Vittoria.Entities.Requerimiento>> ListAsync(string? estado, int? idEspecialidad, int? idProyecto);
        Task<object?> GetAsync(int idRequerimiento);
        Task<int> CrearAsync(Cobranzas_Vittoria.Dtos.Compras.RequerimientoCreateDto dto);
        Task UpdateAsync(int idRequerimiento, int idUsuarioActual, Cobranzas_Vittoria.Dtos.Compras.RequerimientoUpdateDto dto);
        Task ActualizarCantidadesAlmacenAsync(int idRequerimiento, Cobranzas_Vittoria.Dtos.Compras.Requerimientos.ActualizarCantidadesAlmacenRequest request); // marcado para borrar luego

        // nuevos contratos
        Task AgregarItemAsync(int idRequerimiento, Cobranzas_Vittoria.Dtos.Compras.Requerimientos.AgregarItemRequerimientoRequest request);
        Task ActualizarItemAsync(int idRequerimiento, int idItem, Cobranzas_Vittoria.Dtos.Compras.Requerimientos.ActualizarItemRequerimientoRequest request);
        Task EliminarItemAsync(int idRequerimiento, int idItem);


        Task<bool> PuedeEditarAsync(int idRequerimiento);
        Task EnviarAsync(int idRequerimiento, int idUsuarioActual, string? observacion);
        Task ProcesarStockAsync(int idRequerimiento, int idUsuarioActual, string resultado, string? observacion);
        Task AprobarAsync(int idRequerimiento, int idUsuarioActual, string? observacion);
        Task RechazarAsync(int idRequerimiento, int idUsuarioActual, string? observacion);
        Task EnviarComprasAsync(int idRequerimiento, int idUsuarioActual, string? observacion);
    }
}
