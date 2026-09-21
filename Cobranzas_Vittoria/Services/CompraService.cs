using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services
{
    public class CompraService : ICompraService
    {
        private readonly ICompraRepository _repo;

        public CompraService(ICompraRepository repo)
        {
            _repo = repo;
        }

        public Task<IEnumerable<dynamic>> ListAsync(bool? aceptada, int? idProveedor) => _repo.ListAsync(aceptada, idProveedor);
        public Task<object?> GetAsync(int idCompra) => _repo.GetAsync(idCompra);

        public async Task<object> CrearAsync(CompraCreateDto dto)
        {
            var res = await _repo.CrearAsync(dto);
            return new { idCompra = res.IdCompra, montoTotal = res.MontoTotal };
        }

        public Task AceptarAsync(int idCompra, int? idUsuario, string? observacion)
            => _repo.AceptarAsync(idCompra, idUsuario, observacion);

        public Task<IEnumerable<dynamic>> ListPendientesDesdeOcAsync() => _repo.ListPendientesDesdeOcAsync();
        public Task<IEnumerable<dynamic>> GetDocumentosAsync(int idCompra) => _repo.GetDocumentosAsync(idCompra);
        public Task SaveDocumentosAsync(int idCompra, IEnumerable<(string NombreArchivo, string RutaArchivo, string? Extension)> docs) => _repo.SaveDocumentosAsync(idCompra, docs);
    }
}
