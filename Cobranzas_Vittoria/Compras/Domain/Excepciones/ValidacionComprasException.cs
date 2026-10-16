namespace Cobranzas_Vittoria.Compras.Domain.Excepciones;

/// <summary>Un dato requerido está ausente o su valor no es válido para el dominio.</summary>
public sealed class ValidacionComprasException : ComprasException
{
    public ValidacionComprasException(string codigoError, string mensaje)
        : base(codigoError, mensaje) { }

    public static ValidacionComprasException CampoRequerido(string campo)
        => new("CAMPO_REQUERIDO", $"{campo} es obligatorio.");

    public static ValidacionComprasException IdentificadorInvalido(string campo)
        => new("IDENTIFICADOR_INVALIDO", $"{campo} debe ser un identificador positivo.");

    public static ValidacionComprasException Longitud(string campo, int maximo)
        => new("LONGITUD_EXCEDIDA", $"{campo} admite como máximo {maximo} caracteres.");
}
