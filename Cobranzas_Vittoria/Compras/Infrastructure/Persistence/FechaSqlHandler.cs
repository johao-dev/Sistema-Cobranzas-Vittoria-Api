using System.Data;
using Dapper;

namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence;

/// <summary>SqlClient entrega SQL DATE como DateTime; el dominio utiliza fechas sin hora.</summary>
internal sealed class FechaSqlHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => value is DateOnly fecha ? fecha : DateOnly.FromDateTime((DateTime)value);
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }
}
