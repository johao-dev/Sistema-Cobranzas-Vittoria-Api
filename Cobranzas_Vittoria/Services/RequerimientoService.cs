using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Dtos.Compras.Requerimientos;
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

        public async Task ActualizarCantidadesAlmacenAsync(
            int idRequerimiento,
            ActualizarCantidadesAlmacenRequest request)
        {
            if (request.Items is null || request.Items.Count == 0)
            {
                throw new ValidacionNegocioComprasException(
                    "items",
                    "REQUERIMIENTO_ITEMS_REQUERIDOS",
                    "Debe enviar al menos un detalle para actualizar.");
            }

            if (request.Items.Any(item => item.IdRequerimientoDetalle <= 0))
            {
                throw new ValidacionNegocioComprasException(
                    "idRequerimientoDetalle",
                    "REQUERIMIENTO_DETALLE_INVALIDO",
                    "Los identificadores de detalle deben ser mayores que cero.");
            }

            if (request.Items
                .GroupBy(item => item.IdRequerimientoDetalle)
                .Any(group => group.Count() > 1))
            {
                throw new ValidacionNegocioComprasException(
                    "items",
                    "REQUERIMIENTO_DETALLE_DUPLICADO",
                    "No se permiten identificadores de detalle repetidos.");
            }

            if (request.Items.Any(item => item.Cantidad <= 0))
            {
                throw new ValidacionNegocioComprasException(
                    "cantidad",
                    "REQUERIMIENTO_CANTIDAD_INVALIDA",
                    "Todas las cantidades deben ser mayores que cero.");
            }

            await _repo.ActualizarCantidadesAlmacenAsync(idRequerimiento, request.Items);
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
