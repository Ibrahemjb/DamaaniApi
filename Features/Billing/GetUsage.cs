using DammaniAPI.Database;
using DammaniAPI.Features;
using MediatR;

namespace DammaniAPI.Features.Billing;

public class GetUsage
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public int Used { get; set; }
        public int Limit { get; set; }
        public int Percent { get; set; }
        public string Level { get; set; } = "normal";
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public bool Blocked { get; set; }
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
            await SubscriptionRoller.RollIfNeededAsync(db, null, request.ShopId, DateTime.UtcNow);
            var usage = await UsageService.GetUsageAsync(db, null, request.ShopId);

            return new Result
            {
                Success = true,
                Used = usage.Used,
                Limit = usage.Limit,
                Percent = usage.Percent,
                Level = usage.Level,
                PeriodStart = usage.PeriodStart,
                PeriodEnd = usage.PeriodEnd,
                Blocked = usage.Blocked
            };
        }
    }
}
