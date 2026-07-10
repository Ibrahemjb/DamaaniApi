using Dapper;
using MySql.Data.MySqlClient;
using System.Data;

namespace DammaniAPI.Features.ServiceRequests;

// Shared SR-{yyMM}-{seq} number generation (DMN-504/603). Extracted from
// public submit so internal creation stays on the same global sequence.
public static class ServiceRequestNumberHelper
{
    public static string FormatRequestNumber(string monthPrefix, int sequence)
        => $"{monthPrefix}{sequence:0000}";

    public static async Task<string> InsertAsync(IDbConnection db, IDbTransaction tx, object serviceRequest, bool setConsentAt)
    {
        var monthPrefix = $"SR-{DateTime.UtcNow:yyMM}-";
        var nextSequence = await db.ExecuteScalarAsync<int?>(
            "SELECT MAX(CAST(SUBSTRING(RequestNumber, 9) AS UNSIGNED)) FROM ServiceRequest WHERE RequestNumber LIKE @Pattern",
            new { Pattern = monthPrefix + "%" }, tx) ?? 0;

        var consentSql = setConsentAt ? "UTC_TIMESTAMP()" : "NULL";
        for (var attempt = 0; ; attempt++)
        {
            nextSequence++;
            var requestNumber = FormatRequestNumber(monthPrefix, nextSequence);
            var parameters = new DynamicParameters(serviceRequest);
            parameters.Add("RequestNumber", requestNumber);
            try
            {
                await db.ExecuteAsync(
                    $"""
                    INSERT INTO ServiceRequest
                        (Id, ShopId, WarrantyId, RequestNumber, CustomerName, CustomerPhone,
                         ProblemType, Description, PreferredContact, Status, Source, ConsentAt)
                    VALUES
                        (@Id, @ShopId, @WarrantyId, @RequestNumber, @CustomerName, @CustomerPhone,
                         @ProblemType, @Description, @PreferredContact, @Status, @Source, {consentSql})
                    """,
                    parameters, tx);
                return requestNumber;
            }
            catch (MySqlException ex) when (ex.Number == 1062 && attempt < 5)
            {
                // Concurrent submission took this number; retry with next.
            }
        }
    }
}
