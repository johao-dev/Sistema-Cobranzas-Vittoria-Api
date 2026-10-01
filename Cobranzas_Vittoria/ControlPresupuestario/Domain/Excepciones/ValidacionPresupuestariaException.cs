namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

/// <summary>Solicitud inválida para el módulo (HTTP 400): falta un dato o un valor no es admitido.</summary>
public class ValidacionPresupuestariaException : ControlPresupuestarioException
{
    public ValidacionPresupuestariaException(string codigoError, string mensaje) : base(codigoError, mensaje) { }

    public static ValidacionPresupuestariaException CampoRequerido(string campo)
        => new("CAMPO_REQUERIDO", $"{campo} es obligatorio.");

    public static ValidacionPresupuestariaException Longitud(string campo, int maximo)
        => new("LONGITUD_EXCEDIDA", $"{campo} admite como máximo {maximo} caracteres.");

    public static ValidacionPresupuestariaException IdentificadorInvalido(string campo)
        => new("IDENTIFICADOR_INVALIDO", $"{campo} debe ser un identificador positivo.");
}
