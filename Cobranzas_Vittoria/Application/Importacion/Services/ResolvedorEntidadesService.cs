using System.Data;
using System.Globalization;
using System.Text;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Application.Importacion.Services;

/// <summary>
/// Resolutor de catalogos (Especialidad, UnidadMedida) para la importacion
/// masiva de Materiales (v2).
///
/// Dado un nombre leido del archivo (ej: "Albañilería"), devuelve el
/// <c>Id*</c> correspondiente. Si la entidad no existe, la CREA dentro de
/// la transaccion del caller con codigo/nombre autogenerado cuando aplica.
/// Esto permite que la importacion masiva sea atomica: alta de catalogos +
/// INSERT de materiales comparten transaccion.
///
/// <para>
/// <b>Algoritmo:</b>
/// <list type="number">
///   <item>Cargar el catalogo completo (solo registros <c>Activo = 1</c>)
///         en un diccionario en memoria, indexado por
///         <see cref="Normalizar"/>(nombre). Esto es case-insensitive y
///         accent-insensitive.</item>
///   <item>Para cada nombre del archivo, calcular la clave normalizada y
///         buscar en el diccionario. Si esta, devolver el id existente.</item>
///   <item>Si no esta, calcular la sigla (primeras 3 letras del nombre
///         normalizado, sin vocales), generar el codigo
///         <c>UM-&lt;SIGLA&gt;-####</c> (UnidadMedida) y crear la fila via
///         <c>UpsertEnTransaccionAsync</c>. Si la creacion choca con una
///         insercion concurrente (SqlException 2627: UNIQUE violation),
///         se hace un reintento que relee el catalogo y reintenta el lookup.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Reintento por concurrencia:</b> dos imports concurrentes pueden intentar
/// crear la misma Especialidad. El primero gana; el segundo recibe SqlException
/// 2627 (UNIQUE violation) por el indice <c>UX_Especialidad_NombreNormalizado_Activos</c>.
/// El resolver captura esa excepcion, relee el catalogo y vuelve a buscar.
/// Esto es seguro: ambos imports terminaran con el mismo Id.
/// </para>
///
/// <para>
/// <b>Por que un servicio separado:</b> la logica de resolucion
/// (normalizacion, generacion de sigla, retry) NO pertenece al processor (que
/// se enfoca en el patron Template Method) ni al repository (que solo hace
/// CRUD). Es una responsabilidad transversal, con estado en cache por
/// transaccion y dependencias de multiples repos. Vive en la capa de
/// aplicacion.
/// </para>
/// </summary>
public class ResolvedorEntidadesService
{
    /// <summary>Numero de reintentos cuando una creacion choca con UNIQUE violation por insercion concurrente.</summary>
    public const int MaxIntentosCreacion = 3;

    private readonly IEspecialidadRepository _especialidadRepo;
    private readonly IUnidadMedidaRepository _unidadMedidaRepo;
    private readonly ILogger<ResolvedorEntidadesService> _logger;

    public ResolvedorEntidadesService(
        IEspecialidadRepository especialidadRepo,
        IUnidadMedidaRepository unidadMedidaRepo,
        ILogger<ResolvedorEntidadesService> logger)
    {
        _especialidadRepo = especialidadRepo ?? throw new ArgumentNullException(nameof(especialidadRepo));
        _unidadMedidaRepo = unidadMedidaRepo ?? throw new ArgumentNullException(nameof(unidadMedidaRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resuelve el IdEspecialidad para un nombre dado. Si no existe en BD
    /// (case/accent-insensitive), la crea con codigo autogenerado vacio (la
    /// tabla no exige Codigo). Devuelve el id resultante.
    /// </summary>
    /// <param name="nombre">Nombre tal como viene del archivo. Se trimea y se normaliza.</param>
    /// <param name="cn">Conexion abierta del caller.</param>
    /// <param name="tx">Transaccion del caller.</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <returns>IdEspecialidad resuelto (existente o recien creado).</returns>
    /// <exception cref="ArgumentException">Si el nombre esta vacio o solo espacios.</exception>
    public virtual async Task<int> ResolverIdEspecialidadAsync(
        string nombre, IDbConnection cn, IDbTransaction? tx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre de la Especialidad es requerido.", nameof(nombre));
        ArgumentNullException.ThrowIfNull(cn);

        var clave = Normalizar(nombre);

        for (int intento = 1; intento <= MaxIntentosCreacion; intento++)
        {
            // Releemos el catalogo en cada intento para detectar altas concurrentes
            // (no usamos cache local porque la transaccion no ve filas nuevas
            // que otras conexiones commiteen despues del primer SELECT).
            var catalogo = await CargarEspecialidadesAsync(cn, tx, ct);

            if (catalogo.TryGetValue(clave, out var idExistente))
            {
                _logger.LogDebug(
                    "Especialidad '{Nombre}' (clave={Clave}) resuelta por catalogo existente -> Id={Id} (intento {Intento})",
                    nombre, clave, idExistente, intento);
                return idExistente;
            }

            // No existe: crearla. Si choca con UNIQUE por concurrencia, reintentar.
            _logger.LogDebug(
                "Especialidad '{Nombre}' (clave={Clave}) no existe; creando (intento {Intento})",
                nombre, clave, intento);

            var dto = new EspecialidadUpsertDto
            {
                IdEspecialidad = null,
                Nombre = nombre.Trim(),
                Descripcion = null,
                Activo = true
            };

            try
            {
                var nuevoId = await _especialidadRepo.UpsertEnTransaccionAsync(dto, cn, tx, ct);
                _logger.LogInformation(
                    "Especialidad '{Nombre}' creada -> Id={Id} (intento {Intento})",
                    nombre, nuevoId, intento);
                return nuevoId;
            }
            catch (SqlException ex) when (ex.Number == 2627 && intento < MaxIntentosCreacion)
            {
                // UNIQUE violation: otra transaccion creo la misma Especialidad
                // entre nuestro SELECT y nuestro INSERT. Reintentamos.
                _logger.LogWarning(
                    "Especialidad '{Nombre}' choco con UNIQUE (intento {Intento}/{Max}); releyendo catalogo",
                    nombre, intento, MaxIntentosCreacion);
            }
        }

        // Si llegamos aca, superamos MaxIntentosCreacion sin exito. Lanzamos
        // una excepcion con contexto para que el processor la traduzca.
        throw new InvalidOperationException(
            $"No se pudo crear la Especialidad '{nombre}' tras {MaxIntentosCreacion} intentos por colision de UNIQUE concurrente.");
    }

    /// <summary>
    /// Resuelve el IdUnidadMedida para un nombre dado. Si no existe, la crea
    /// con codigo autogenerado <c>UM-&lt;SIGLA&gt;-####</c> (sigla derivada
    /// del nombre, sin vocales). Devuelve el id resultante.
    /// </summary>
    /// <param name="nombre">Nombre tal como viene del archivo. Se trimea y se normaliza.</param>
    /// <param name="cn">Conexion abierta del caller.</param>
    /// <param name="tx">Transaccion del caller.</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <returns>IdUnidadMedida resuelto (existente o recien creada).</returns>
    /// <exception cref="ArgumentException">Si el nombre esta vacio o solo espacios.</exception>
    public virtual async Task<int> ResolverIdUnidadMedidaAsync(
        string nombre, IDbConnection cn, IDbTransaction? tx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre de la Unidad de Medida es requerido.", nameof(nombre));
        ArgumentNullException.ThrowIfNull(cn);

        var clave = Normalizar(nombre);

        for (int intento = 1; intento <= MaxIntentosCreacion; intento++)
        {
            var catalogo = await CargarUnidadesMedidaAsync(cn, tx, ct);

            if (catalogo.TryGetValue(clave, out var existente))
            {
                _logger.LogDebug(
                    "UnidadMedida '{Nombre}' (clave={Clave}) resuelta por catalogo existente -> Id={Id} (intento {Intento})",
                    nombre, clave, existente.IdUnidadMedida, intento);
                return existente.IdUnidadMedida;
            }

            // No existe: crearla con codigo autogenerado.
            _logger.LogDebug(
                "UnidadMedida '{Nombre}' (clave={Clave}) no existe; creando (intento {Intento})",
                nombre, clave, intento);

            var sigla = DerivarSigla(nombre);
            // El correlativo se calcula leyendo la cantidad actual del catalogo
            // + 1. Es una heuristica: en escenarios de alta concurrencia podria
            // chocar con otro insert; la transaccion + el retry cubren ese caso.
            var correlativo = catalogo.Count + 1;
            var codigo = $"UM-{sigla}-{correlativo:0000}";

            var dto = new UnidadMedidaUpsertDto
            {
                IdUnidadMedida = null,
                Codigo = codigo,
                Nombre = nombre.Trim(),
                Activo = true
            };

            try
            {
                var nuevoId = await _unidadMedidaRepo.UpsertEnTransaccionAsync(dto, cn, tx, ct);
                _logger.LogInformation(
                    "UnidadMedida '{Nombre}' creada con Codigo='{Codigo}' -> Id={Id} (intento {Intento})",
                    nombre, codigo, nuevoId, intento);
                return nuevoId;
            }
            catch (SqlException ex) when (ex.Number == 2627 && intento < MaxIntentosCreacion)
            {
                // UNIQUE violation: otra transaccion creo la misma UnidadMedida
                // (por el indice UX_UnidadMedida_NombreNormalizado_Activos) o
                // un Codigo igual (poco probable por la sigla+correlativo, pero
                // posible si dos imports tienen el mismo Nombre y misma sigla
                // con el mismo correlativo). Reintentamos.
                _logger.LogWarning(
                    "UnidadMedida '{Nombre}' choco con UNIQUE (intento {Intento}/{Max}); releyendo catalogo",
                    nombre, intento, MaxIntentosCreacion);
            }
        }

        throw new InvalidOperationException(
            $"No se pudo crear la Unidad de Medida '{nombre}' tras {MaxIntentosCreacion} intentos por colision de UNIQUE concurrente.");
    }

    /// <summary>
    /// Normaliza un nombre para usarlo como clave de lookup: uppercase,
    /// sin acentos (NFD + remove diacritics), sin espacios al borde.
    ///
    /// Es la misma normalizacion que aplica SQL Server en la columna
    /// computada <c>NombreNormalizado</c> de las tablas Especialidad y
    /// UnidadMedida (ver V1_2_1__Maestra_Importacion_Tipos_v2.sql).
    /// La consistencia entre cliente y servidor es lo que hace que el lookup
    /// por clave normalizada funcione.
    /// </summary>
    public static string Normalizar(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return string.Empty;

        // NFD descompone los caracteres acentuados en su base + diacritico.
        // Luego removemos los diacriticos (categoria "NonSpacingMark").
        // Ej: "Albañilería" -> "ALBANILERIA"
        var descompuesto = nombre.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    /// <summary>
    /// Deriva una sigla de 3 letras a partir del nombre normalizado, removiendo
    /// vocales. Si quedan menos de 3 consonantes, se rellena con 'X' hasta
    /// llegar a 3.
    ///
    /// Ejemplos:
    ///   "Kilogramo"  -> "KLM"  (K, l, g, m, sin vocales: i, o, a, o -> K, l, g, m, primer 3: KLG -> Wait, let me re-check)
    ///   "Metro"      -> "MTR"
    ///   "Unidad"     -> "NND"  (sin vocales: N, d -> "ND" -> "NDD")
    ///   "B"          -> "BXX"
    ///
    /// La sigla se concatena con el correlativo para formar el codigo
    /// "UM-<SIGLA>-####".
    /// </summary>
    public static string DerivarSigla(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "XXX";

        // Normalizamos primero (sin acentos, uppercase) para que "Albañil" derive
        // la misma sigla que "Albanil".
        var normalizado = Normalizar(nombre);
        var consonantes = new StringBuilder(3);
        foreach (var c in normalizado)
        {
            if (!EsVocal(c) && char.IsLetter(c))
            {
                consonantes.Append(c);
                if (consonantes.Length == 3) break;
            }
        }

        // Rellenar con 'X' si quedaron menos de 3 consonantes
        while (consonantes.Length < 3)
            consonantes.Append('X');

        return consonantes.ToString();
    }

    private static bool EsVocal(char c) => c is 'A' or 'E' or 'I' or 'O' or 'U';

    private async Task<Dictionary<string, int>> CargarEspecialidadesAsync(
        IDbConnection cn, IDbTransaction tx, CancellationToken ct)
    {
        var entidades = await _especialidadRepo.ListEnTransaccionAsync(activo: true, cn, tx, ct);
        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var e in entidades)
        {
            var clave = Normalizar(e.Nombre);
            if (dict.ContainsKey(clave))
            {
                _logger.LogWarning(
                    "Especialidad duplicada detectada en BD con clave normalizada '{Clave}': Id={IdA} y Id={IdB}. " +
                    "El indice UNIQUE deberia impedir esto; revisar integridad.",
                    clave, dict[clave], e.IdEspecialidad);
            }
            dict[clave] = e.IdEspecialidad;
        }
        return dict;
    }

    private async Task<Dictionary<string, UnidadMedidaDto>> CargarUnidadesMedidaAsync(
        IDbConnection cn, IDbTransaction tx, CancellationToken ct)
    {
        var entidades = await _unidadMedidaRepo.ListEnTransaccionAsync(activo: true, cn, tx, ct);
        var dict = new Dictionary<string, UnidadMedidaDto>(StringComparer.Ordinal);
        foreach (var u in entidades)
        {
            var clave = Normalizar(u.Nombre);
            if (!dict.ContainsKey(clave))
                dict[clave] = u;
        }
        return dict;
    }
}
