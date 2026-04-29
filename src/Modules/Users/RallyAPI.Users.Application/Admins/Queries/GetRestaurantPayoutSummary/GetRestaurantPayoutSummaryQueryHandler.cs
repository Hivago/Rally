using MediatR;
using RallyAPI.SharedKernel.Abstractions.Payouts;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetRestaurantPayoutSummary;

internal sealed class GetRestaurantPayoutSummaryQueryHandler
    : IRequestHandler<GetRestaurantPayoutSummaryQuery, Result<RestaurantPayoutSummary>>
{
    private static readonly TimeZoneInfo IstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly IAdminRepository _adminRepository;
    private readonly IAdminPayoutQueryService _payouts;

    public GetRestaurantPayoutSummaryQueryHandler(
        IAdminRepository adminRepository,
        IAdminPayoutQueryService payouts)
    {
        _adminRepository = adminRepository;
        _payouts = payouts;
    }

    public async Task<Result<RestaurantPayoutSummary>> Handle(
        GetRestaurantPayoutSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<RestaurantPayoutSummary>(Error.NotFound("Admin", request.RequestedByAdminId));

        var nextAutoRunAt = NextMondaySixAmIstAsUtc(DateTime.UtcNow);
        var summary = await _payouts.GetRestaurantSummaryAsync(nextAutoRunAt, cancellationToken);

        return Result.Success(summary);
    }

    /// <summary>
    /// Mirrors WeeklyPayoutBatchService schedule (Mon 06:00 IST) so the dashboard shows the
    /// same time the job will actually fire next.
    /// </summary>
    private static DateTime NextMondaySixAmIstAsUtc(DateTime nowUtc)
    {
        var nowIst = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, IstTimeZone);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)nowIst.DayOfWeek + 7) % 7;
        var candidate = nowIst.Date.AddDays(daysUntilMonday).AddHours(6);
        if (candidate <= nowIst)
            candidate = candidate.AddDays(7);
        return TimeZoneInfo.ConvertTimeToUtc(candidate, IstTimeZone);
    }
}
