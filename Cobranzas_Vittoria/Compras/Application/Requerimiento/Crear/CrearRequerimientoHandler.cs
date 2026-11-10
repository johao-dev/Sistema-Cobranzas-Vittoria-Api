using System.Globalization;
using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using RequerimientoDomain = Cobranzas_Vittoria.Compras.Domain.Model.Requerimiento;

namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;

public sealed class CrearRequerimientoHandler(
    IRequerimientoRepository repository,
    IConsultaCreacionRequerimiento consultas,
    IUsuarioActualService usuarioActual,
    TimeProvider reloj,
    ILogger<CrearRequerimientoHandler> logger)
{
    private const int MAXIMO_INTENTOS = 5;

    public async Task<CrearRequerimientoResult> HandleAsync(CrearRequerimientoCommand command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);

        int idSolicitante = usuarioActual.EstaAutenticado && usuarioActual.IdUsuario is > 0
            ? usuarioActual.IdUsuario.Value
            : throw new AutenticacionException();

        IReadOnlyList<ItemRequerimiento> items = CrearRequerimientoValidator.ValidarEntrada(command);
        DateOnly fecha = command.FechaRequerimiento == default
            ? DateOnly.FromDateTime(reloj.GetLocalNow().DateTime)
            : command.FechaRequerimiento;

        int[] materiales = items.Select(i => i.IdMaterial).ToArray();
        int[] partidas = items.Where(i => i.IdPresupuestoDetalle.HasValue)
            .Select(i => i.IdPresupuestoDetalle!.Value).Distinct().ToArray();

        ContextoCreacionRequerimiento contexto = await consultas.ObtenerContextoAsync(
            command.IdProyecto, idSolicitante, materiales, partidas, ct);
        CrearRequerimientoValidator.ValidarContexto(contexto, command.IdProyecto, items);

        string numeroSolicitado = command.NumeroRequerimiento?.Trim() ?? string.Empty;

        for (int intento = 1; intento <= MAXIMO_INTENTOS; intento++)
        {
            ct.ThrowIfCancellationRequested();

            DisponibilidadNumeroRequerimiento disponibilidad = await consultas.ObtenerNumeracionAsync(numeroSolicitado, ct);
            string numero = ResolverNumero(numeroSolicitado, disponibilidad);
            RequerimientoDomain requerimiento = RequerimientoDomain.Crear(numero, fecha, command.IdProyecto,
                idSolicitante, items, command.Descripcion, command.FechaEntrega, command.Observacion);

            try
            {
                int id = await repository.CrearAsync(requerimiento, ct);
                logger.LogInformation("Requerimiento {IdRequerimiento} creado por el usuario {IdSolicitante}", id, idSolicitante);
                return new CrearRequerimientoResult(id);
            }
            catch (NumeroRequerimientoOcupadoException) when (intento < MAXIMO_INTENTOS)
            {
                // Solo esta colisión acredita rollback del SP. Un timeout puede tener commit desconocido.
                logger.LogInformation("Colisión de numeración al crear requerimiento; reintento {Intento}", intento);
            }
        }

        throw new InvalidOperationException("Se agotaron los intentos de creación.");
    }

    private static string ResolverNumero(string solicitado, DisponibilidadNumeroRequerimiento disponibilidad)
    {
        if (solicitado.Length > 0 && !disponibilidad.NumeroSolicitadoExiste)
            return solicitado;

        if (disponibilidad.MaximoNumero >= int.MaxValue)
            throw new ConflictoComprasException("REQUERIMIENTO_NUMERACION_AGOTADA", "Se agotó el rango de numeración automática del requerimiento.");

        return (disponibilidad.MaximoNumero + 1).ToString(CultureInfo.InvariantCulture);
    }
}
