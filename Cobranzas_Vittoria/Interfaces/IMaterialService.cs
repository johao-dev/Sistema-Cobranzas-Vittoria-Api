namespace Cobranzas_Vittoria.Interfaces
{
    public interface IMaterialService
    {
        Task<IEnumerable<Cobranzas_Vittoria.Entities.Material>> ListAsync(bool? activo, int? idEspecialidad);
        Task<Cobranzas_Vittoria.Entities.Material?> GetAsync(int idMaterial);
        Task<string> GetSiguienteCodigoAsync();
        Task<int> UpsertAsync(Cobranzas_Vittoria.Dtos.Maestra.MaterialUpsertDto dto);
    }
}
