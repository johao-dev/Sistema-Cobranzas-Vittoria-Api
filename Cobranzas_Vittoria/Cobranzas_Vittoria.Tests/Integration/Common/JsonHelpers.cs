using System.Text.Json;

namespace Cobranzas_Vittoria.Tests.Integration.Common;

/// <summary>
/// Helpers para inspeccionar respuestas JSON con tolerancia al casing.
///
/// En este proyecto conviven dos estilos de respuesta según el repository:
///
///   1) Repos que proyectan a entidades POCO (ej. OrdenCompraRepository) ->
///      System.Text.Json aplica la politica camelCase por defecto.
///      JSON: { "idOrdenCompra": 1, "estado": "Registrada", ... }
///
///   2) Repos que retornan IEnumberable&lt;dynamic&gt; o DapperRow directo
///      (ej. CompraRepository, KardexRepository) ->
///      System.Text.Json NO transforma las claves de diccionario,
///      asi que las propiedades conservan el casing del SQL (PascalCase).
///      JSON: { "IdCompra": 1, "Aceptada": false, "IdOrdenCompra": 1, ... }
///
/// Para no acoplarnos al estilo concreto, estos helpers intentan primero el
/// nombre tal cual y, si no existe, hacen una busqueda case-insensitive sobre
/// las propiedades del elemento.
/// </summary>
public static class JsonHelpers
{
    /// <summary>
    /// Obtiene una propiedad del JsonElement con busqueda tolerante al casing.
    /// Lanza KeyNotFoundException con un mensaje claro si no existe.
    /// </summary>
    public static JsonElement GetProp(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException(
                $"Se esperaba un objeto JSON pero se recibio {element.ValueKind}.");

        if (element.TryGetProperty(propertyName, out var match))
            return match;

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }

        var disponibles = string.Join(", ",
            element.EnumerateObject().Select(p => p.Name));
        throw new KeyNotFoundException(
            $"No se encontro la propiedad '{propertyName}' en el JSON. " +
            $"Propiedades disponibles: [{disponibles}]");
    }

    /// <summary>
    /// Indica si la propiedad existe (case-insensitive).
    /// </summary>
    public static bool HasProp(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        if (element.TryGetProperty(propertyName, out _)) return true;
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static string GetString(JsonElement element, string propertyName)
        => GetProp(element, propertyName).GetString()
           ?? throw new InvalidOperationException(
               $"La propiedad '{propertyName}' es null o no es un string.");

    public static int GetInt32(JsonElement element, string propertyName)
        => GetProp(element, propertyName).GetInt32();

    public static long GetInt64(JsonElement element, string propertyName)
        => GetProp(element, propertyName).GetInt64();

    public static decimal GetDecimal(JsonElement element, string propertyName)
        => GetProp(element, propertyName).GetDecimal();

    public static bool GetBoolean(JsonElement element, string propertyName)
        => GetProp(element, propertyName).GetBoolean();

    public static DateTime GetDateTime(JsonElement element, string propertyName)
        => GetProp(element, propertyName).GetDateTime();
}
