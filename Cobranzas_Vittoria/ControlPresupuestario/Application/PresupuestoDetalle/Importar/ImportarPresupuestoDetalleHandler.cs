using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;
using Cobranzas_Vittoria.Application.Importacion;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Importar;

/// <summary>
/// Carga de montos de una versión desde CSV/XLSX. Columnas: CodigoPartida y Monto (requeridas),
/// Observacion (opcional); la columna Partida de la plantilla es informativa. Todo o nada: con un
/// error en cualquier fila no se carga ningún monto y se informan todos los errores juntos (422).
/// </summary>
public sealed class ImportarPresupuestoDetalleHandler
{
    private static readonly string[] ColumnasRequeridas = { "CodigoPartida", "Monto" };

    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;
    private readonly ICatalogoPartidaRepository _partidas;
    private readonly ILectorArchivoTabular _lector;
    private readonly ILogger<ImportarPresupuestoDetalleHandler> _logger;

    public ImportarPresupuestoDetalleHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles,
        ICatalogoPartidaRepository partidas, ILectorArchivoTabular lector, ILogger<ImportarPresupuestoDetalleHandler> logger)
    {
        _versiones = versiones;
        _detalles = detalles;
        _partidas = partidas;
        _lector = lector;
        _logger = logger;
    }

    public async Task<CargaLoteResult> HandleAsync(ImportarPresupuestoDetalleCommand command)
    {
        PresupuestoVersionValidator.ValidarIds(command.IdPresupuesto, command.IdPresupuestoVersion);
        var version = await _versiones.ObtenerDelPresupuestoAsync(command.IdPresupuesto, command.IdPresupuestoVersion);
        if (!version.EsBorrador)
            throw new ValidacionPresupuestariaException("VERSION_NO_EDITABLE",
                "Solo se pueden cargar montos en una versión en BORRADOR. Crea una nueva versión para modificar montos.");

        var filas = _lector.Leer(command.Archivo);
        if (filas.Count == 0)
            throw new DatosInvalidosException(
                "El archivo no contiene filas de datos (solo encabezados o está vacío).", Array.Empty<DetalleErrorFila>());
        var encabezados = filas[0].Columnas.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var faltantes = ColumnasRequeridas.Where(c => !encabezados.Contains(c)).ToArray();
        if (faltantes.Length > 0)
            throw new EstructuraInvalidaException(CodigosError.Estructura.EncabezadosIncorrectos,
                $"Faltan las columnas requeridas: {string.Join(", ", faltantes)}. " +
                $"Encabezados recibidos: {string.Join(", ", encabezados)}.");

        var partidas = (await _partidas.ListarAsync(new FiltroPartidas()))
            .ToDictionary(p => p.Codigo, StringComparer.OrdinalIgnoreCase);
        var errores = new List<DetalleErrorFila>();
        var items = new List<LotePresupuestario.Item>(filas.Count);
        var vistas = new Dictionary<int, int>();
        foreach (var fila in filas)
        {
            var n = fila.NumeroFila;
            var codigo = fila.Valor("CodigoPartida");
            var montoTexto = fila.Valor("Monto");
            var observacion = fila.Valor("Observacion");

            Domain.Model.CatalogoPartida? partida = null;
            if (string.IsNullOrEmpty(codigo))
                errores.Add(new(n, "CodigoPartida", CodigosError.Fila.CampoRequerido, "La columna CodigoPartida es obligatoria."));
            else if (!partidas.TryGetValue(codigo, out partida))
                errores.Add(new(n, "CodigoPartida", CodigosError.Sp.FkNoExiste, $"La partida {codigo} no existe en el catálogo."));
            else if (!partida.Activo)
                errores.Add(new(n, "CodigoPartida", CodigosError.Fila.ReglaNegocio, $"La partida {codigo} está inactiva."));
            else if (!partida.EsHoja)
                errores.Add(new(n, "CodigoPartida", CodigosError.Fila.ReglaNegocio,
                    $"La partida {codigo} tiene partidas hijas; solo las partidas sin hijas reciben montos."));
            else if (vistas.TryGetValue(partida.IdCatalogoPartida, out var primera))
                errores.Add(new(n, "CodigoPartida", CodigosError.Sp.ValorDuplicadoEnArchivo,
                    $"La partida {codigo} ya aparece en la fila {primera}."));
            else
                vistas[partida.IdCatalogoPartida] = n;

            decimal monto = 0;
            if (string.IsNullOrEmpty(montoTexto))
                errores.Add(new(n, "Monto", CodigosError.Fila.CampoRequerido,
                    "La columna Monto es obligatoria. Si la partida no aplica, escribe 0."));
            else if (!MontoImportado.TryLeer(montoTexto, out monto))
                errores.Add(new(n, "Monto", CodigosError.Fila.FormatoInvalido, $"El monto '{montoTexto}' no es un número válido."));
            else if (monto < 0)
                errores.Add(new(n, "Monto", CodigosError.Fila.ReglaNegocio, "El monto no puede ser negativo."));
            else if (decimal.Round(monto, 2) != monto)
                errores.Add(new(n, "Monto", CodigosError.Fila.FormatoInvalido, "El monto admite como máximo 2 decimales."));

            if (observacion is { Length: > 500 })
                errores.Add(new(n, "Observacion", CodigosError.Fila.FormatoInvalido,
                    "La observación admite como máximo 500 caracteres."));

            if (partida is not null)
                items.Add(new LotePresupuestario.Item(partida.IdCatalogoPartida, monto, observacion, n));
        }

        if (errores.Count > 0)
            throw new DatosInvalidosException($"El archivo contiene {errores.Count} error(es). No se cargó ningún monto.",
                errores.OrderBy(e => e.Fila).ThenBy(e => e.Campo).ToList());

        var resultado = await _detalles.CargarLoteAsync(LotePresupuestario.Crear(command.IdPresupuestoVersion, items,
            command.QuitarAusentes));
        _logger.LogInformation("Presupuesto importado en la versión {Version}: {Partidas} partidas, total {Total}",
            command.IdPresupuestoVersion, resultado.PartidasEnVersion, resultado.MontoTotal);
        return CargaLoteResult.Desde(resultado);
    }
}
