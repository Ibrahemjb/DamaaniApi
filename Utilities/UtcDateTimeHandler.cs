using System.Data;
using Dapper;

namespace DammaniAPI.Utilities;

public class UtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override void SetValue(IDbDataParameter parameter, DateTime value)
        => parameter.Value = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    public override DateTime Parse(object value)
        => DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
}
