using System.Globalization;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Validators;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Importar;

/// <summary>Valida todo el Excel antes de delegar la persistencia atómica al repositorio de gastos.</summary>
public sealed class ImportarGastosDirectosHandler
{
    private readonly IGastoDirectoRepository _gastos;
    private readonly ICatalogoPartidaRepository _partidas;
    private readonly IProveedorRepository _proveedores;
    private readonly ICategoriaGastoRepository _categorias;
    private readonly ICatalogoControlPresupuestarioRepository _catalogos;
    private readonly FileValidator _validador;
    private readonly FileParserResolver _parsers;
    private readonly IUsuarioActualService _usuario;
    private readonly ILogger<ImportarGastosDirectosHandler> _logger;

    public ImportarGastosDirectosHandler(IGastoDirectoRepository gastos, ICatalogoPartidaRepository partidas,
        IProveedorRepository proveedores, ICategoriaGastoRepository categorias, ICatalogoControlPresupuestarioRepository catalogos,
        FileValidator validador, FileParserResolver parsers, IUsuarioActualService usuario,
        ILogger<ImportarGastosDirectosHandler> logger)
        => (_gastos, _partidas, _proveedores, _categorias, _catalogos, _validador, _parsers, _usuario, _logger) =
            (gastos, partidas, proveedores, categorias, catalogos, validador, parsers, usuario, logger);

    public async Task<ResultadoImportacionGastosDirectos> HandleAsync(IFormFile archivo, CancellationToken ct = default)
    {
        _validador.Validar(archivo);
        if (!string.Equals(Path.GetExtension(archivo.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArchivoInvalidoException("EXTENSION_INVALIDA", "La plantilla de gastos directos debe ser .xlsx.");
        var parser = _parsers.ObtenerParser(archivo) as ExcelFileParser
            ?? throw new EstructuraInvalidaException("FORMATO_INVALIDO", "El contenido debe ser un libro XLSX.");
        var filas = parser.ParsePlantilla(archivo, PlantillaGastosDirectos.Hoja, 5,
            PlantillaGastosDirectos.Encabezados, PlantillaGastosDirectos.Auxiliares);
        if (filas.Count is 0 or > PlantillaGastosDirectos.MaximoRegistros)
            throw new DatosInvalidosException("El archivo debe contener entre 1 y 200 registros no vacíos.",
                filas.Select(f => new DetalleErrorFila(f.NumeroFila, "Archivo", "LIMITE_REGISTROS",
                    "El archivo excede el máximo de 200 registros.")).ToArray(), filas.Count);

        ct.ThrowIfCancellationRequested();
        var categorias = (await _categorias.ListAsync(null)).ToArray();
        var centros = await _gastos.ListarCentrosCostoAsync();
        var partidas = await _partidas.ListarAsync(new FiltroPartidas());
        var proveedores = (await _proveedores.ListAsync(null, null)).ToArray();
        var monedas = await _catalogos.ListarMonedasAsync(null);
        var disponibles = new Dictionary<int, IReadOnlyList<PartidaDisponibleGasto>>();
        var errores = new List<DetalleErrorFila>();
        var lote = new List<GastoDirectoImportado>();
        foreach (var fila in filas)
        {
            ct.ThrowIfCancellationRequested();
            var antes = errores.Count;
            string V(string campo) => fila.GetString(campo)?.Trim() ?? string.Empty;
            void Error(string campo, string motivo, string codigo = "REGLA_NEGOCIO")
                => errores.Add(new(fila.NumeroFila, campo, codigo, $"Fila {fila.NumeroFila}: {motivo}"));
            foreach (var celda in fila.Celdas.Where(c => c.Value is "#CELDA_INVALIDA" or "#FECHA_INVALIDA" or "#NUMERO_INVALIDO"))
                Error(celda.Key, $"{celda.Key} contiene una celda o fórmula inválida.", "FORMATO_INVALIDO");
            foreach (var campo in new[] { "Categoría código", "Centro de costo", "Partida", "Fecha", "Concepto", "Proveedor", "Monto" })
                if (V(campo).Length == 0) Error(campo, $"{campo} es obligatorio.", "CAMPO_REQUERIDO");

            var codigo = V("Categoría código");
            var encontradas = categorias.Where(c => c.Codigo == codigo).ToArray();
            var categoria = encontradas.Length == 1 ? encontradas[0] : null;
            if (codigo.Length > 0)
            {
                if (!PlantillaGastosDirectos.Categorias.Contains(codigo)) Error("Categoría código", $"la categoría \"{codigo}\" no está permitida.");
                else if (encontradas.Length == 0) Error("Categoría código", $"la categoría \"{codigo}\" no existe.");
                else if (encontradas.Length > 1) Error("Categoría código", $"la categoría \"{codigo}\" es ambigua.");
                else if (!categoria!.Activo) Error("Categoría código", $"la categoría \"{codigo}\" está inactiva.");
            }

            var centrosEncontrados = centros.Where(c => Igual(c.Codigo, V("Centro de costo")) || Igual(c.Nombre, V("Centro de costo"))).ToArray();
            if (V("Centro de costo").Length > 0 && centrosEncontrados.Length != 1)
                Error("Centro de costo", centrosEncontrados.Length > 1 ? $"el centro de costo \"{V("Centro de costo")}\" es ambiguo. Usa su código."
                    : $"el centro de costo \"{V("Centro de costo")}\" no existe o no está habilitado.");
            var centro = centrosEncontrados.Length == 1 ? centrosEncontrados[0] : null;
            var partidasEncontradas = partidas.Where(p => Igual(p.Codigo, V("Partida")) || Igual(p.Nombre, V("Partida"))).ToArray();
            if (V("Partida").Length > 0 && partidasEncontradas.Length == 0)
                Error("Partida", $"la partida \"{V("Partida")}\" no existe.");
            PartidaDisponibleGasto? detalle = null;
            if (centro is not null && partidasEncontradas.Length > 0)
            {
                if (!disponibles.TryGetValue(centro.IdCentroCosto, out var opciones))
                    disponibles[centro.IdCentroCosto] = opciones = await _gastos.ListarPartidasDisponiblesAsync(centro.IdCentroCosto);
                var matches = opciones.Where(p => partidasEncontradas.Any(cp => cp.IdCatalogoPartida == p.IdCatalogoPartida)).ToArray();
                if (matches.Length == 1) detalle = matches[0];
                else Error("Partida", matches.Length > 1 ? $"la partida \"{V("Partida")}\" es ambigua en el centro \"{V("Centro de costo")}\"."
                    : $"la partida \"{V("Partida")}\" no pertenece al centro de costo \"{V("Centro de costo")}\" o no está disponible en un presupuesto aprobado y activo.");
            }

            var proveedoresEncontrados = proveedores.Where(p => Igual(p.Ruc, V("Proveedor")) || Igual(p.RazonSocial, V("Proveedor"))).ToArray();
            var proveedor = proveedoresEncontrados.Length == 1 ? proveedoresEncontrados[0] : null;
            if (V("Proveedor").Length > 0)
            {
                if (proveedoresEncontrados.Length == 0) Error("Proveedor", $"el proveedor \"{V("Proveedor")}\" no existe.");
                else if (proveedoresEncontrados.Length > 1) Error("Proveedor", $"el proveedor \"{V("Proveedor")}\" es ambiguo. Usa su RUC.");
                else if (!proveedor!.Activo) Error("Proveedor", $"el proveedor \"{V("Proveedor")}\" está inactivo.");
            }
            if (detalle is not null && !monedas.Any(m => m.IdMoneda == detalle.IdMoneda && m.Activo))
                Error("Partida", "la moneda del presupuesto no está activa.");

            decimal Numero(string campo, int escala)
            {
                var texto = V(campo);
                if (texto.Length == 0) return 0;
                if (!decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                        CultureInfo.InvariantCulture, out var valor))
                    Error(campo, $"{campo} \"{texto}\" no es un decimal válido.", "FORMATO_INVALIDO");
                else if (valor <= 0) Error(campo, $"{campo} debe ser mayor que cero; recibido \"{texto}\".");
                else if (decimal.Round(valor, escala) != valor || valor >= (escala == 2 ? 10000000000000000m : 1000000000000m))
                    Error(campo, $"{campo} excede la precisión decimal(18,{escala}).", "FORMATO_INVALIDO");
                return valor;
            }
            DateTime Fecha(string campo)
            {
                var texto = V(campo);
                if (!DateTime.TryParseExact(texto, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha) && texto.Length > 0)
                    Error(campo, $"{campo} \"{texto}\" no es una fecha válida en formato dd/MM/yyyy.", "FORMATO_INVALIDO");
                return fecha;
            }
            var monto = Numero("Monto", 2);
            var fecha = Fecha("Fecha");
            var extranjero = new[] { "Moneda factura", "Monto factura", "Tipo de cambio", "Fecha tipo cambio" };
            var tieneReferencia = extranjero.Any(c => V(c).Length > 0);
            int? monedaOriginal = null;
            decimal? montoOriginal = null, cambio = null;
            DateTime? fechaCambio = null;
            if (tieneReferencia)
            {
                foreach (var campo in extranjero.Where(c => V(c).Length == 0))
                    Error(campo, $"se informó información de moneda extranjera, pero falta {campo}.", "CAMPO_REQUERIDO");
                if (V("Moneda factura").Length > 0 && V("Moneda factura") != "USD")
                    Error("Moneda factura", $"la moneda \"{V("Moneda factura")}\" no está permitida; usa USD.");
                var originales = monedas.Where(m => m.Codigo == V("Moneda factura") && m.Activo).ToArray();
                if (V("Moneda factura").Length > 0 && originales.Length != 1)
                    Error("Moneda factura", $"la moneda \"{V("Moneda factura")}\" no existe, está inactiva o es ambigua.");
                monedaOriginal = originales.Length == 1 ? originales[0].IdMoneda : null;
                montoOriginal = Numero("Monto factura", 2);
                cambio = Numero("Tipo de cambio", 6);
                fechaCambio = Fecha("Fecha tipo cambio");
                if (detalle is not null && monedaOriginal == detalle.IdMoneda)
                    Error("Moneda factura", "la moneda original debe ser distinta de la moneda del presupuesto.");
            }
            foreach (var (campo, maximo) in new[] { ("Concepto", 250), ("Descripción", 500) })
                if (V(campo).Length > maximo) Error(campo, $"{campo} admite como máximo {maximo} caracteres.", "FORMATO_INVALIDO");
            var facturas = V("Factura(s)").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (facturas.Any(f => f.Length > 255) || (V("Factura(s)").Length > 0 && facturas.Length == 0))
                Error("Factura(s)", "indica números de factura de hasta 255 caracteres separados por ';'.", "FORMATO_INVALIDO");
            if (facturas.Distinct(StringComparer.OrdinalIgnoreCase).Count() != facturas.Length)
                Error("Factura(s)", "una factura está repetida en la misma fila.");
            if (errores.Count != antes) continue;
            try
            {
                var registro = RegistroGastoDirecto.Crear(detalle!.IdPresupuestoDetalle, categoria!.IdCategoriaGasto,
                    proveedor!.IdProveedor, detalle.IdMoneda, fecha, V("Concepto"), V("Descripción"), monto,
                    monedaOriginal, montoOriginal, cambio, fechaCambio);
                lote.Add(new(fila.NumeroFila, centro!.IdCentroCosto, registro, facturas));
            }
            catch (GastoDirectoException ex) { Error("Moneda factura", ex.Message, ex.CodigoError); }
        }
        if (errores.Count > 0)
            throw new DatosInvalidosException("El lote contiene errores. No se importó ningún gasto.", errores, filas.Count);
        var usuario = _usuario.ObtenerUsuarioActual();
        await _gastos.CrearLoteAsync(lote, usuario, ct);
        _logger.LogInformation("Gastos directos importados: {Cantidad}; Usuario={Usuario}", lote.Count, usuario);
        return new("gastos-directos", "xlsx", lote.Count, filas.Count, filas.Count, 0, true, "IMPORTADO", Array.Empty<DetalleErrorFila>());
    }

    private static bool Igual(string? a, string b) => b.Length > 0 && string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
}
