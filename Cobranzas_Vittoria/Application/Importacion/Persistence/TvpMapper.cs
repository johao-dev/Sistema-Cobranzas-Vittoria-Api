using System.Data;
using System.Reflection;

namespace Cobranzas_Vittoria.Application.Importacion.Persistence;

/// <summary>
/// Convierte <see cref="IEnumerable{TDto}"/> a un <see cref="DataTable"/> compatible con el
/// TVP correspondiente en SQL Server.
///
/// Estrategia:
///   1. Lee las propiedades publicas de instancia de <typeparamref name="TDto"/> una sola vez
///      (con cache por tipo, para evitar el costo de reflexion en cada llamada).
///   2. Por cada propiedad, agrega una columna al <see cref="DataTable"/> con el .NET type
///      correspondiente. Dapper infiere el <c>SqlDbType</c> correcto a partir del .NET type
///      cuando envia el DataTable como parametro TVP.
///   3. Por cada DTO, agrega una fila mapeando valor por valor, convirtiendo <c>null</c> a <see cref="DBNull"/>.
///
/// El nombre de cada columna es el nombre de la propiedad C# (PascalCase). Esto requiere
/// que las columnas del TVP en SQL Server se llamen igual que las propiedades del DTO
/// (convencion que mantenemos en todos los DTOs de importacion).
///
/// <para>
/// <b>Tipos soportados</b> (los tipos .NET que se mapean 1-a-1 a tipos SQL estandar):
/// <list type="bullet">
///   <item><c>string</c>           -&gt; <c>NVARCHAR</c> (Dapper infiere el tamano del TVP destino)</item>
///   <item><c>int</c> / <c>int?</c> -&gt; <c>INT</c></item>
///   <item><c>long</c> / <c>long?</c> -&gt; <c>BIGINT</c></item>
///   <item><c>decimal</c> / <c>decimal?</c> -&gt; <c>DECIMAL</c></item>
///   <item><c>bool</c> / <c>bool?</c> -&gt; <c>BIT</c></item>
///   <item><c>DateTime</c> / <c>DateTime?</c> -&gt; <c>DATETIME</c></item>
///   <item><c>Guid</c> / <c>Guid?</c> -&gt; <c>UNIQUEIDENTIFIER</c></item>
///   <item><c>double</c> / <c>double?</c> -&gt; <c>FLOAT</c></item>
///   <item><c>float</c> / <c>float?</c> -&gt; <c>REAL</c></item>
/// </list>
/// </para>
///
/// Esta clase es <c>static</c> y sin estado: es segura para uso concurrente.
/// </summary>
public static class TvpMapper
{
    // Cache de propiedades por tipo para evitar reflexion repetida.
    // ConcurrentDictionary garantiza thread-safety sin lock.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyInfo[]> _propCache = new();

    /// <summary>
    /// Convierte la coleccion de DTOs a un <see cref="DataTable"/> con columnas nombradas
    /// igual que las propiedades del DTO. Si la coleccion esta vacia, devuelve un
    /// <see cref="DataTable"/> con las columnas definidas pero sin filas.
    /// </summary>
    /// <typeparam name="TDto">Tipo del DTO. Sus propiedades publicas definen las columnas.</typeparam>
    /// <param name="dtos">DTOs a convertir.</param>
    /// <returns>DataTable listo para enviar como parametro TVP.</returns>
    public static DataTable ToDataTable<TDto>(IEnumerable<TDto> dtos) where TDto : class
    {
        var properties = _propCache.GetOrAdd(typeof(TDto), t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        var dataTable = new DataTable();
        foreach (var prop in properties)
        {
            // Nullable.GetUnderlyingType: si la propiedad es int? (Nullable<int>), usamos int.
            // Asi DataTable.Columns acepta tanto int como int? sin lanzar.
            var columnType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            dataTable.Columns.Add(prop.Name, columnType);
        }

        if (dtos is null) return dataTable;

        foreach (var dto in dtos)
        {
            if (dto is null) continue; // Saltar nulos defensivamente
            var row = dataTable.NewRow();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(dto);
                row[prop.Name] = value ?? DBNull.Value;
            }
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }
}
