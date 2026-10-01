namespace Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

/// <summary>Regla de gasto directo rechazada (HTTP 409). Los SPs contable.usp_GastoDirecto_* la emiten en el rango 515xx.</summary>
public class GastoDirectoException : Exception
{
    public string CodigoError { get; }

    public GastoDirectoException(string codigoError, string mensaje) : base(mensaje) => CodigoError = codigoError;
}

/// <summary>Solicitud inválida (HTTP 400). Conserva el código SOLICITUD_INVALIDA del contrato HTTP de gastos directos.</summary>
public sealed class ValidacionGastoDirectoException : GastoDirectoException
{
    public const string Codigo = "SOLICITUD_INVALIDA";

    public ValidacionGastoDirectoException(string mensaje) : base(Codigo, mensaje) { }
}

/// <summary>Gasto directo o documento inexistente (HTTP 404).</summary>
public sealed class GastoDirectoNoEncontradoException : GastoDirectoException
{
    public GastoDirectoNoEncontradoException(string mensaje) : base("RECURSO_NO_ENCONTRADO", mensaje) { }

    public static GastoDirectoNoEncontradoException Gasto(int id) => new($"El gasto directo {id} no existe.");

    public static GastoDirectoNoEncontradoException Documento(int id) => new($"El documento {id} no existe en el gasto indicado.");
}
