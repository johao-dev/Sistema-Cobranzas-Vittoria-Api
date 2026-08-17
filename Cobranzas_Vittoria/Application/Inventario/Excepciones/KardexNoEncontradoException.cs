namespace Cobranzas_Vittoria.Application.Inventario.Excepciones;

/// <summary>
/// Excepcion que se lanza cuando no se encuentra un kardex por su Id
/// (ya sea entrada o salida). Mapea a HTTP 404 Not Found.
///
/// <para>
/// <b>Por que no se usa <c>KeyNotFoundException</c> del framework</b>:
/// queremos un tipo especifico del dominio para que el controller (o un
/// futuro middleware) pueda distinguir "kardex no existe" de cualquier
/// otra <c>KeyNotFoundException</c> del sistema, sin acoplar el catch
/// al mensaje.
/// </para>
///
/// <para>
/// <b>Por que es <c>sealed</c></b>: no esta prevista una jerarquia de
/// excepciones "no encontrado" en este modulo. Si en el futuro se
/// necesita una base comun, se introduce <c>NotFoundExceptionBase</c>
/// y se reabre la herencia.
/// </para>
/// </summary>
public sealed class KardexNoEncontradoException : Exception
{
    /// <summary>Codigo legible del error, devuelto en el campo <c>error</c> de la respuesta 404.</summary>
    public const string CodigoError = "KARDEX_NO_ENCONTRADO";

    /// <summary>Tipo de kardex que no se encontro ("entrada" o "salida"). Solo informativo, para el mensaje.</summary>
    public string TipoKardex { get; }

    /// <summary>Id que se buscaba cuando se produjo el error.</summary>
    public int IdKardex { get; }

    public KardexNoEncontradoException(string tipoKardex, int idKardex)
        : base($"No se encontro el kardex de {tipoKardex} con Id={idKardex}.")
    {
        TipoKardex = tipoKardex;
        IdKardex = idKardex;
    }
}
