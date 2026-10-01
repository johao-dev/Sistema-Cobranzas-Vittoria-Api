using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Cobranzas_Vittoria.Application.Importacion;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Importar;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.ImportarEstructura;

/// <summary>Fila del presupuesto jerárquico ya leída. EsCategoria: otra fila del archivo la tiene como padre.</summary>
public sealed record FilaEstructura(int Fila, string Codigo, string? CodigoPadre, string Nombre, string? MontoTexto,
    decimal? Monto, string? Tipo, string? Seccion, string? Observacion, bool EsCategoria);

/// <summary>
/// Lee un presupuesto jerárquico (CSV/XLSX) y valida su estructura sin mirar la base.
/// <list type="bullet">
/// <item>Código: columna <c>Codigo</c> ("1.1.3") o columnas <c>Nivel 1</c>…<c>Nivel N</c>; un 0 o una celda vacía cierran el código.</item>
/// <item><c>Nombre</c> (o <c>Descripcion</c>) obligatorio; <c>Monto</c> (o <c>Total</c>) obligatorio en las hojas (0 permitido,
/// redondeado al céntimo) y vacío o 0 en las categorías, cuyo total siempre es la suma de sus hojas.</item>
/// <item><c>Tipo</c>, <c>Seccion</c> y <c>Observacion</c> opcionales; el resto de columnas (Und., Metrado, Precio, Subtotal) se ignora.</item>
/// </list>
/// Los encabezados se comparan sin mayúsculas, tildes, espacios ni puntos.
/// </summary>
public static class EstructuraArchivo
{
    public const string NivelesInvalidos = "NIVELES_INVALIDOS";
    public const string PadreConMonto = "PADRE_CON_MONTO";
    public const string PadreNoExiste = "PADRE_NO_EXISTE";
    public const string JerarquiaDistinta = "JERARQUIA_DISTINTA";
    public const string NombreDistinto = "NOMBRE_DISTINTO";

    private static readonly Regex ColumnaNivel = new(@"^nivel(\d+)$", RegexOptions.CultureInvariant);

    public static (IReadOnlyList<FilaEstructura> Filas, List<DetalleErrorFila> Errores) Leer(IReadOnlyList<FilaTabular> filas)
    {
        if (filas.Count == 0)
            throw new DatosInvalidosException(
                "El archivo no contiene filas de datos (solo encabezados o está vacío).", Array.Empty<DetalleErrorFila>());

        var columnas = filas[0].Columnas
            .GroupBy(NormalizarEncabezado)
            .ToDictionary(g => g.Key, g => g.First());
        string? Columna(params string[] alias) => alias.Select(a => columnas.GetValueOrDefault(a)).FirstOrDefault(c => c is not null);

        var codigo = Columna("codigo", "codigopartida");
        var niveles = columnas
            .Select(c => (Match: ColumnaNivel.Match(c.Key), Columna: c.Value))
            .Where(c => c.Match.Success)
            .OrderBy(c => int.Parse(c.Match.Groups[1].Value, CultureInfo.InvariantCulture))
            .Select(c => c.Columna)
            .ToList();
        var nombre = Columna("nombre", "descripcion", "partida");
        var monto = Columna("monto", "total");
        var faltantes = new List<string>();
        if (codigo is null && niveles.Count == 0) faltantes.Add("Codigo (o Nivel 1, Nivel 2, …)");
        if (nombre is null) faltantes.Add("Nombre (o Descripcion)");
        if (monto is null) faltantes.Add("Monto (o Total)");
        if (faltantes.Count > 0)
            throw new EstructuraInvalidaException(CodigosError.Estructura.EncabezadosIncorrectos,
                $"Faltan las columnas requeridas: {string.Join(", ", faltantes)}. " +
                $"Encabezados recibidos: {string.Join(", ", filas[0].Columnas)}.");
        var tipo = Columna("tipo", "tipopartida");
        var seccion = Columna("seccion", "secciongasto");
        var observacion = Columna("observacion", "observaciones");

        var errores = new List<DetalleErrorFila>();
        var leidas = new List<(int Fila, string Codigo, string Nombre, string? Monto, string? Tipo, string? Seccion, string? Obs)>();
        foreach (var fila in filas)
        {
            var n = fila.NumeroFila;
            var cod = codigo is not null ? LeerCodigo(fila.Valor(codigo), n, errores) : CodigoDesdeNiveles(fila, niveles, n, errores);
            var nom = Normalizar(fila.Valor(nombre!));
            // Una fila totalmente vacía (típica al final de un Excel) no es un error.
            if (cod is null && string.IsNullOrEmpty(nom) && string.IsNullOrEmpty(fila.Valor(monto!)))
            {
                errores.RemoveAll(e => e.Fila == n);
                continue;
            }
            if (cod is null) continue;
            if (cod.Length > 50)
                errores.Add(new(n, "Codigo", CodigosError.Fila.FormatoInvalido, "El código admite como máximo 50 caracteres."));
            if (string.IsNullOrEmpty(nom))
                errores.Add(new(n, "Nombre", CodigosError.Fila.CampoRequerido, "El nombre de la partida es obligatorio."));
            else if (nom.Length > 200)
                errores.Add(new(n, "Nombre", CodigosError.Fila.FormatoInvalido, "El nombre admite como máximo 200 caracteres."));
            var obs = observacion is null ? null : NullSiVacio(fila.Valor(observacion));
            if (obs is { Length: > 500 })
                errores.Add(new(n, "Observacion", CodigosError.Fila.FormatoInvalido, "La observación admite como máximo 500 caracteres."));
            leidas.Add((n, cod, nom, NullSiVacio(fila.Valor(monto!)), tipo is null ? null : NullSiVacio(fila.Valor(tipo)),
                seccion is null ? null : NullSiVacio(fila.Valor(seccion)), obs));
        }

        var primera = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in leidas)
            if (!primera.TryAdd(f.Codigo, f.Fila))
                errores.Add(new(f.Fila, "Codigo", CodigosError.Sp.ValorDuplicadoEnArchivo,
                    $"El código {f.Codigo} ya aparece en la fila {primera[f.Codigo]}. Si es una partida hija, revisa sus niveles."));
        var padres = leidas.Select(f => Padre(f.Codigo)).Where(p => p is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resultado = new List<FilaEstructura>(leidas.Count);
        foreach (var f in leidas)
        {
            var esCategoria = padres.Contains(f.Codigo);
            decimal? valor = null;
            if (f.Monto is not null)
            {
                if (!MontoImportado.TryLeer(f.Monto, out var m))
                    errores.Add(new(f.Fila, "Monto", CodigosError.Fila.FormatoInvalido, $"El monto '{f.Monto}' no es un número válido."));
                else if (esCategoria && m != 0)
                    errores.Add(new(f.Fila, "Monto", PadreConMonto,
                        $"La partida {f.Codigo} es una categoría (tiene partidas hijas): deja el monto vacío o en 0; " +
                        "su total se calcula como la suma de sus hijas."));
                else if (m < 0)
                    errores.Add(new(f.Fila, "Monto", CodigosError.Fila.ReglaNegocio, "El monto no puede ser negativo."));
                else
                    // Los totales de un Excel suelen ser fórmulas con muchos decimales: se redondean al céntimo.
                    valor = decimal.Round(m, 2, MidpointRounding.AwayFromZero);
            }
            if (f.Seccion is not null && esCategoria)
                errores.Add(new(f.Fila, "Seccion", CodigosError.Fila.ReglaNegocio,
                    $"La partida {f.Codigo} es una categoría; solo una partida sin hijas puede tener sección."));
            resultado.Add(new FilaEstructura(f.Fila, f.Codigo, Padre(f.Codigo), f.Nombre, f.Monto, valor, f.Tipo, f.Seccion,
                f.Obs, esCategoria));
        }
        return (resultado, errores);
    }

    /// <summary>Código del padre: el código sin su último segmento ("1.1.3" → "1.1"); null en una raíz.</summary>
    public static string? Padre(string codigo)
    {
        var i = codigo.LastIndexOf('.');
        return i > 0 ? codigo[..i] : null;
    }

    /// <summary>Compara nombres sin mayúsculas, tildes ni espacios repetidos.</summary>
    public static bool MismoNombre(string a, string b)
        => string.Equals(SinTildes(Normalizar(a)), SinTildes(Normalizar(b)), StringComparison.OrdinalIgnoreCase);

    private static string? LeerCodigo(string? valor, int n, List<DetalleErrorFila> errores)
    {
        var codigo = NullSiVacio(valor);
        if (codigo is null) return null;
        codigo = codigo.Replace(" ", string.Empty).Trim('.');
        if (codigo.Split('.').Any(s => s.Length == 0))
        {
            errores.Add(new(n, "Codigo", CodigosError.Fila.FormatoInvalido, $"El código '{valor}' tiene segmentos vacíos."));
            return null;
        }
        return codigo;
    }

    private static string? CodigoDesdeNiveles(FilaTabular fila, IReadOnlyList<string> niveles, int n, List<DetalleErrorFila> errores)
    {
        var segmentos = new List<string>();
        var cerrado = false;
        foreach (var columna in niveles)
        {
            var texto = NullSiVacio(fila.Valor(columna));
            var valor = 0m;
            if (texto is not null && (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor)
                || valor < 0 || valor != decimal.Truncate(valor)))
            {
                errores.Add(new(n, columna, NivelesInvalidos, $"El valor '{texto}' de {columna} debe ser un número entero."));
                return null;
            }
            if (valor == 0) { cerrado = true; continue; }
            if (cerrado)
            {
                errores.Add(new(n, columna, NivelesInvalidos,
                    $"{columna} tiene valor después de un nivel vacío o en 0: los niveles deben ser consecutivos."));
                return null;
            }
            segmentos.Add(decimal.Truncate(valor).ToString("0", CultureInfo.InvariantCulture));
        }
        if (segmentos.Count == 0)
        {
            errores.Add(new(n, niveles[0], NivelesInvalidos, $"La fila no tiene código: {niveles[0]} está vacío o en 0."));
            return null;
        }
        return string.Join('.', segmentos);
    }

    private static string NormalizarEncabezado(string encabezado)
        => new(SinTildes(encabezado).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string Normalizar(string? texto) => Regex.Replace(texto ?? string.Empty, @"\s+", " ").Trim();

    private static string? NullSiVacio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string SinTildes(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        return new string(descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC);
    }
}
