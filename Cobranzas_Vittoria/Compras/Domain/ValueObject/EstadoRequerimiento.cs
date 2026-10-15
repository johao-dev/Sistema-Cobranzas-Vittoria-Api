namespace Cobranzas_Vittoria.Compras.Domain.ValueObject;

public static class EstadoRequerimiento
{
    public const string Registrado = "Registrado";
    public const string EnviadoAlmacen = "EnviadoAlmacen";
    public const string ValidadoAlmacen = "ValidadoAlmacen";
    public const string Rechazado = "Rechazado";
    public const string AprobadoCoordinador = "AprobadoCoordinador";
    public const string EnviadoOC = "EnviadoOC";
    public const string GeneradoOC = "GeneradoOC";
    // Se reconoce al reconstruir históricos. No se inventa una transición de anulación sin un flujo definido.
    public const string Anulado = "Anulado";
}
