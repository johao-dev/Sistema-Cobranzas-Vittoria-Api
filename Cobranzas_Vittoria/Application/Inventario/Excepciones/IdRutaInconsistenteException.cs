namespace Cobranzas_Vittoria.Application.Inventario.Excepciones;

/// <summary>
/// Excepcion que se lanza cuando el id de la ruta no coincide con el id
/// del cuerpo de la peticion en operaciones PUT (KardexEntrada / KardexSalida).
/// Mapea a HTTP 400 Bad Request.
///
/// <para>
/// <b>Por que se valida en el controller y no en el service</b>:
/// la convencion del modulo Inventario
/// es que la validacion <c>idRuta == dto.idKardex*</c> se hace en el
/// controller para mantener el service libre de dependencias con la
/// <c>HttpContext</c>. El controller atrapa esta excepcion y devuelve 400
/// antes de delegar al service.
/// </para>
///
/// <para>
/// <b>Por que es una excepcion y no un return value</b>:
/// mantiene el mismo flujo que el resto de errores de validacion
/// (el middleware las traduce a JSON) y permite que el controller
/// sea declarativo (sin logica de if/else por cada endpoint PUT).
/// </para>
/// </summary>
public sealed class IdRutaInconsistenteException : Exception
{
    /// <summary>Codigo legible del error, devuelto en el campo <c>error</c> de la respuesta 400.</summary>
    public const string CodigoError = "ID_RUTA_INCONSISTENTE";

    /// <summary>Id que venia en la ruta (ej: <c>/api/almacen/kardex/entradas/{id}</c>).</summary>
    public int IdRuta { get; }

    /// <summary>Id que venia en el cuerpo del DTO.</summary>
    public int? IdCuerpo { get; }

    /// <summary>Nombre del campo del cuerpo que se esperaba coincidir (ej: "idKardexEntrada").</summary>
    public string CampoCuerpo { get; }

    public IdRutaInconsistenteException(int idRuta, int? idCuerpo, string campoCuerpo)
        : base($"El id de la ruta ({idRuta}) no coincide con {campoCuerpo} del cuerpo ({(idCuerpo.HasValue ? idCuerpo.Value.ToString() : "null")}).")
    {
        IdRuta = idRuta;
        IdCuerpo = idCuerpo;
        CampoCuerpo = campoCuerpo;
    }
}
