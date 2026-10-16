using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Application.Compras.Excepciones;

namespace Cobranzas_Vittoria.Services
{
    public class RequerimientoService : IRequerimientoService
    {
        private readonly IRequerimientoRepository _repo;

        public RequerimientoService(IRequerimientoRepository repo)
        {
            _repo = repo;
        }

        public Task<IEnumerable<Requerimiento>> ListAsync(string? estado, int? idEspecialidad, int? idProyecto)
            => _repo.ListAsync(estado, idEspecialidad, idProyecto);

        public async Task<object?> GetAsync(int idRequerimiento)
        {
            var (head, items, validaciones) = await _repo.GetAsync(idRequerimiento);
            if (head is null) return null;

            var puedeEditar = await _repo.PuedeEditarAsync(idRequerimiento);

            return new
            {
                requerimiento = head,
                items,
                validaciones,
                puedeEditar
            };
        }

        public Task<int> CrearAsync(RequerimientoCreateDto dto) => _repo.CrearAsync(dto);

        public async Task UpdateAsync(int idRequerimiento, int idUsuarioActual, RequerimientoUpdateDto dto)
        {
            await ValidarSolicitanteAsync(idRequerimiento, idUsuarioActual);
            await _repo.UpdateAsync(idRequerimiento, dto);
        }

        public Task<bool> PuedeEditarAsync(int idRequerimiento) => _repo.PuedeEditarAsync(idRequerimiento);
        public Task EnviarAsync(int idRequerimiento, int idUsuarioActual, string? observacion) => _repo.EnviarAsync(idRequerimiento, idUsuarioActual, observacion);
        public Task ProcesarStockAsync(int idRequerimiento, int idUsuarioActual, string resultado, string? observacion) => _repo.ProcesarStockAsync(idRequerimiento, idUsuarioActual, resultado, observacion);
        public Task AprobarAsync(int idRequerimiento, int idUsuarioActual, string? observacion) => _repo.AprobarAsync(idRequerimiento, idUsuarioActual, observacion);
        public Task RechazarAsync(int idRequerimiento, int idUsuarioActual, string? observacion) => _repo.RechazarAsync(idRequerimiento, idUsuarioActual, observacion);
        public Task EnviarComprasAsync(int idRequerimiento, int idUsuarioActual, string? observacion) => _repo.EnviarComprasAsync(idRequerimiento, idUsuarioActual, observacion);

        private async Task ValidarSolicitanteAsync(int idRequerimiento, int idUsuarioActual)
        {
            if (!await _repo.EsSolicitanteAsync(idRequerimiento, idUsuarioActual))
            {
                throw new ValidacionNegocioComprasException(
                    "idRequerimiento",
                    "REQUERIMIENTO_SOLICITANTE_REQUERIDO",
                    "Solo el usuario solicitante puede modificar el requerimiento.");
            }
        }
    }
}
