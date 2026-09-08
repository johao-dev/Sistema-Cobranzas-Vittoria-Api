namespace Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

/// <summary>Representa credenciales o refresh tokens inválidos sin exponer su causa.</summary>
public sealed class AutenticacionException : Exception
{
    public const string CodigoError = "CREDENCIALES_INVALIDAS";

    public AutenticacionException() : base("Las credenciales proporcionadas no son válidas.") { }
}
