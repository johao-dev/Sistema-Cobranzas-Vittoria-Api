using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;

/// <summary>
/// Helper para construir <see cref="SqlException"/> en tests unitarios
/// con un <c>Number</c> y <c>Message</c> configurables.
///
/// <para>
/// <b>Por que este helper existe</b>: <see cref="SqlException"/> es sealed
/// y su propiedad <c>Number</c> no se puede asignar directamente. La
/// propiedad devuelve el <c>Number</c> del primer elemento de un
/// <c>SqlErrorCollection</c> privado. Ambos tipos
/// (<c>SqlErrorCollection</c> y <c>SqlError</c>) son <c>internal</c> en
/// <c>Microsoft.Data.SqlClient</c>, pero podemos crearlos con
/// <see cref="RuntimeHelpers.GetUninitializedObject"/> y asignar los
/// campos privados por reflection. Esto es estable entre versiones
/// porque los nombres de campos son internos del assembly y no cambian
/// a menudo.
/// </para>
///
/// <para>
/// <b>Limitacion conocida</b>: <see cref="SqlException.InnerException"/>,
/// <see cref="SqlException.ClientConnectionId"/>, etc. quedan con valores
/// por defecto. Para los tests del modulo Inventario eso es suficiente:
/// solo necesitamos <c>Number</c> y <c>Message</c> para que el
/// <c>SqlExceptionTranslator</c> funcione.
/// </para>
/// </summary>
public static class SqlExceptionBuilder
{
    /// <summary>
    /// Crea una <see cref="SqlException"/> con el <c>Number</c> y
    /// <c>Message</c> indicados. El formato del message puede ser
    /// <c>'CODIGO: detalle'</c> o solo el detalle.
    /// </summary>
    public static SqlException Crear(int number, string message)
    {
        var sqlEx = (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));

        // Asignar _message de Exception para que Message getter funcione.
        var messageField = typeof(Exception).GetField(
            "_message", BindingFlags.Instance | BindingFlags.NonPublic);
        messageField?.SetValue(sqlEx, message);

        // Crear SqlErrorCollection. La coleccion tiene constructor sin
        // parametros y su unico campo (_errors) es de tipo List<object>
        // (no List<SqlError> en la firma del assembly).
        var errorCollectionType = typeof(SqlException).Assembly.GetType(
            "Microsoft.Data.SqlClient.SqlErrorCollection")
            ?? throw new InvalidOperationException("SqlErrorCollection no encontrado.");
        var errorCollection = Activator.CreateInstance(errorCollectionType, nonPublic: true)
            ?? throw new InvalidOperationException("No se pudo instanciar SqlErrorCollection.");

        // Crear un SqlError y setear su campo Number.
        var sqlError = RuntimeHelpers.GetUninitializedObject(
            typeof(SqlException).Assembly.GetType("Microsoft.Data.SqlClient.SqlError")
            ?? throw new InvalidOperationException("SqlError no encontrado."));

        // SqlError en Microsoft.Data.SqlClient 6.x expone el numero como
        // propiedad publica de solo lectura (Number) cuyo backing field es
        // privado "number" (con guion bajo en algunas versiones: _number).
        // Como el campo es readonly-init, GetField lo encuentra igual.
        var numberField = sqlError.GetType().GetField(
            "number", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? sqlError.GetType().GetField(
                "_number", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? sqlError.GetType().GetField(
                "Number", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException(
                "Campo/propiedad Number de SqlError no encontrado.");
        numberField.SetValue(sqlError, number);

        // Asignar el SqlError a la coleccion. SqlErrorCollection._errors
        // es List<object> (en la firma del assembly), asi que creamos
        // un List<object> real, agregamos el SqlError, y lo asignamos.
        var errorsListField = errorCollectionType.GetField(
            "_errors", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Campo _errors de SqlErrorCollection no encontrado.");
        var list = new List<object> { sqlError };
        errorsListField.SetValue(errorCollection, list);

        // Asignar la coleccion a SqlException._errors.
        var errorsField = typeof(SqlException).GetField(
            "_errors", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Campo _errors de SqlException no encontrado.");
        errorsField.SetValue(sqlEx, errorCollection);

        return sqlEx;
    }
}
