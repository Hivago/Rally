using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Riders.Queries.GetEarningsSummary;

internal sealed class GetRiderEarningsSummaryQueryHandler
    : IRequestHandler<GetRiderEarningsSummaryQuery, Result<RiderEarningsSummaryResponse>>
{
    private static readonly TimeZoneInfo IstTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly IRiderPayoutLedgerRepository _ledger;

    public GetRiderEarningsSummaryQueryHandler(IRiderPayoutLedgerRepository ledger)
    {
        _ledger = ledger;
    }

    public async Task<Result<RiderEarningsSummaryResponse>> Handle(
        GetRiderEarningsSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var breakdown = await _ledger.GetEarningsBreakdownAsync(
            request.RiderId, nowUtc, cancellationToken);

        var response = new RiderEarningsSummaryResponse(
            TotalEarned: breakdown.Total,
            PendingPayout: breakdown.Pending,
            ThisWeek: breakdown.ThisWeek,
            ThisMonth: breakdown.ThisMonth,
            NextPayoutDateUtc: NextMondaySixAmIstAsUtc(nowUtc));

        return Result.Success(response);
    }

    /// <summary>
    /// Payouts run weekly via RiderPayoutAggregationJob — Monday 06:00 IST.
    /// Mirrors the cadence used by the admin payout summary.
    /// </summary>
    private static DateTime NextMondaySixAmIstAsUtc(DateTime nowUtc)
    {
        DateTime nowIst = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, IstTimeZone);
        int daysUntilMonday = ((int)DayOfWeek.Monday - (int)nowIst.DayOfWeek + 7) % 7;
        DateTime candidate = nowIst.Date.AddDays(daysUntilMonday).AddHours(6);
        if (candidate <= nowIst)
            candidate = candidate.AddDays(7);

        return TimeZoneInfo.ConvertTimeToUtc(candidate, IstTimeZone);
    }
}
