using System.Data;
using Dapper;

namespace Cobranzas_Vittoria.Application.Common;

/// <summary>
/// <c>TypeHandler</c> de Dapper para mapear <see cref="DateOnly"/> contra
/// columnas/parametros <c>DATE</c> de SQL Server.
///
/// <para>
/// <b>Por que existe</b>:
/// Dapper en <c>Microsoft.Data.SqlClient 6.x</c> NO soporta
/// <see cref="DateOnly"/> de forma nativa:
/// <list type="bullet">
///   <item>Como parametro de Stored Procedure: lanza
///   <c>"The member X of type System.DateOnly cannot be used as a parameter value"</c>.</item>
///   <item>Como propiedad de un DTO de salida: al mapear un <see cref="DateTime"/>
///   (lo que devuelve el driver para una columna <c>DATE</c>) a <see cref="DateOnly"/>
///   lanza <c>"Error parsing column N (Campo=... - DateTime)"</c>.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Convencion</b>:
/// <list type="bullet">
///   <item>Lectura: el driver entrega <see cref="DateTime"/>. Convertimos a
///   <see cref="DateOnly"/> con <see cref="DateOnly.FromDateTime(DateTime)"/>.</item>
///   <item>Escritura: serializamos <see cref="DateOnly"/> como
///   <see cref="DateTime"/> a medianoche (<see cref="TimeOnly.MinValue"/>)
///   para que SQL Server lo interprete como <c>DATE</c>.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Registro</b>: una sola vez en <c>Program.cs</c> via
/// <c>SqlMapper.AddTypeHandler(new DateOnlyTypeHandler())</c>. Dapper lo aplica
/// globalmente a todas las operaciones de la aplicacion.
/// </para>
/// </summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value)
        => value is DateTime dt
            ? DateOnly.FromDateTime(dt)
            : default;

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
        => parameter.Value = value.ToDateTime(TimeOnly.MinValue);
}
