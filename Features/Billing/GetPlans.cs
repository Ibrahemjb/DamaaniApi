using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Billing;

public class GetPlans
{
    public class Query : IRequest<Result>
    {
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public IReadOnlyList<BillingPlan> Plans { get; set; } = Array.Empty<BillingPlan>();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var plans = await PlanQueries.ListActiveAsync(db, null);
            return new Result { Plans = plans };
        }
    }
}
