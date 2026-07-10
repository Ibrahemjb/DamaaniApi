using System.Text;
using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using MediatR;

namespace DammaniAPI.Features.Warranties;

// CSV export of the filtered list (DMN-409, BP §10.11). Plan-gated server-side
// (Plan.HasExport — Pro/Business); reuses WarrantyListFilter so the file always
// matches what the list shows; UTF-8 BOM so Arabic opens correctly in Excel.
public class ExportWarranties
{
    public const int MaxRows = 5000;

    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public DateTime? ExpiryFrom { get; set; }
        public DateTime? ExpiryTo { get; set; }
        public string? BranchId { get; set; }
        public string? CreatedByUserId { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public byte[]? FileBytes { get; set; }
        public string? FileName { get; set; }
        public int RowCount { get; set; }
    }

    private class ExportRow
    {
        public string? Code { get; set; }
        public string? CustomerName { get; set; }
        public string? Phone { get; set; }
        public string? ProductName { get; set; }
        public string? SerialNumber { get; set; }
        public string? Category { get; set; }
        public string? Status { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();

            var hasExport = await db.ExecuteScalarAsync<bool?>(
                """
                SELECT p.HasExport
                FROM Subscription sub
                JOIN Plan p ON p.Id = sub.PlanId
                WHERE sub.ShopId = @ShopId
                """,
                new { request.ShopId });
            if (hasExport != true)
                return new Result { Success = false, ErrorCode = ErrorCodes.FeatureNotInPlan };

            var (whereSql, parameters) = WarrantyListFilter.Build(request.ShopId, new WarrantyListFilter.Args
            {
                Search = request.Search,
                Status = request.Status,
                Category = request.Category,
                CreatedFrom = request.CreatedFrom,
                CreatedTo = request.CreatedTo,
                ExpiryFrom = request.ExpiryFrom,
                ExpiryTo = request.ExpiryTo,
                BranchId = request.BranchId,
                CreatedByUserId = request.CreatedByUserId
            });

            parameters.Add("MaxRows", MaxRows);
            var rows = (await db.QueryAsync<ExportRow>(
                $"""
                SELECT w.Code, c.Name AS CustomerName, c.Phone,
                       w.ProductName, w.SerialNumber, w.Category,
                       {WarrantyListFilter.DerivedStatusSql} AS Status,
                       w.PurchaseDate, w.ExpiryDate, w.CreatedAt
                FROM Warranty w
                JOIN Customer c ON c.Id = w.CustomerId
                WHERE {whereSql}
                ORDER BY w.CreatedAt DESC
                LIMIT @MaxRows
                """,
                parameters)).ToList();

            var csv = BuildCsv(rows);

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "warranty", request.ShopId!, "warranty.list_exported",
                request.ActorUserId,
                System.Text.Json.JsonSerializer.Serialize(new { count = rows.Count }));

            return new Result
            {
                Success = true,
                FileBytes = csv,
                FileName = $"damaani-warranties-{DateTime.UtcNow:yyyy-MM-dd}.csv",
                RowCount = rows.Count
            };
        }

        private static byte[] BuildCsv(List<ExportRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Code,CustomerName,Phone,ProductName,SerialNumber,Category,Status,PurchaseDate,ExpiryDate,CreatedAt");
            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",",
                    Escape(row.Code),
                    Escape(row.CustomerName),
                    Escape(row.Phone),
                    Escape(row.ProductName),
                    Escape(row.SerialNumber),
                    Escape(row.Category),
                    Escape(row.Status),
                    row.PurchaseDate?.ToString("yyyy-MM-dd"),
                    row.ExpiryDate?.ToString("yyyy-MM-dd"),
                    row.CreatedAt.ToString("yyyy-MM-dd HH:mm")));
            }

            // BOM prefix is deliberate: without it Excel misreads Arabic text.
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        internal static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
