namespace Cobranzas_Vittoria.Interfaces
{
    public interface IRolService
    {
        Task<IEnumerable<Cobranzas_Vittoria.Entities.Rol>> ListAsync(bool? activo = null);
        Task<int> UpsertAsync(Cobranzas_Vittoria.Dtos.Seguridad.RolUpsertDto dto);
    }
}
