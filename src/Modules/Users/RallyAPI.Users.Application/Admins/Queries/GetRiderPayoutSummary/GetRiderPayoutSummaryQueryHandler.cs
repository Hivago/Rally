using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetRiderPayoutSummary;

internal sealed class GetRiderPayoutSummaryQueryHandler
    : IRequestHandler<GetRiderPayoutSummaryQuery, Result<RiderPayoutSummary>>
{
    private static readonly TimeZoneInfo IstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly IAdminRepository _adminRepository;
    private readonly IRiderPayoutQueryService _payouts;

    public GetRiderPayoutSummaryQueryHandler(
        IAdminRepository adminRepository,
        IRiderPayoutQueryService payouts)
    {
        _adminRepository = adminRepository;
        _payouts = payouts;
    }

    public async Task<Result<RiderPayoutSummary>> Handle(
        GetRiderPayoutSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<RiderPayoutSummary>(Error.NotFound("Admin", request.RequestedByAdminId));

        DateTime nextAutoRunAt = NextMondaySixAmIstAsUtc(DateTime.UtcNow);
        RiderPayoutSummary summary = await _payouts.GetSummaryAsync(nextAutoRunAt, cancellationToken);

        return Result.Success(summary);
    }

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
