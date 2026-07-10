using System.Data;
using System.Globalization;
using Dapper;

namespace DammaniAPI.Utilities;

public class UtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override void SetValue(IDbDataParameter parameter, DateTime value)
        => parameter.Value = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    public override DateTime Parse(object value)
    {
        // MySQL connector / expressions like GREATEST(..., '1970-01-01') can
        // surface DATETIME as string — accept both.
        var utc = value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.UtcDateTime,
            string s => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture)
        };
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }
}
