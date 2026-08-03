using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetRestaurantEarnings;

public sealed class GetRestaurantEarningsQueryHandler
    : IRequestHandler<GetRestaurantEarningsQuery, Result<EarningsSummaryDto>>
{
    private static readonly TimeZoneInfo IstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly IPayoutLedgerRepository _ledgerRepository;

    public GetRestaurantEarningsQueryHandler(IPayoutLedgerRepository ledgerRepository)
    {
        _ledgerRepository = ledgerRepository;
    }

    public async Task<Result<EarningsSummaryDto>> Handle(
        GetRestaurantEarningsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.FromDate > query.ToDate)
            return Result.Failure<EarningsSummaryDto>(Error.Validation("From date must be before or equal to To date."));

        var periodStart = query.FromDate;
        var periodEnd = query.ToDate;

        // Convert IST date range to UTC for querying. Entries are matched by date
        // regardless of payout-batch status, so past (already-batched/paid) weeks
        // are just as browsable as the current, still-unbatched week.
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(
            periodStart.ToDateTime(TimeOnly.MinValue), IstTimeZone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(
            periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), IstTimeZone);

        var entries = await _ledgerRepository.GetByOwnerIdAndDateRangeAsync(
            query.OwnerId, fromUtc, toUtc, cancellationToken);

        var ledgerDtos = entries.Select(e => new PayoutLedgerDto
        {
            Id = e.Id,
            OutletId = e.OutletId,
            OrderId = e.OrderId,
            OrderNumber = e.OrderNumber,
            OrderAmount = e.OrderAmount,
            GstAmount = e.GstAmount,
            CommissionPercentage = e.CommissionPercentage,
            CommissionFlatFee = e.CommissionFlatFee,
            CommissionAmount = e.CommissionAmount,
            CommissionGst = e.CommissionGst,
            TdsAmount = e.TdsAmount,
            NetAmount = e.NetAmount,
            Status = e.Status,
            PayoutId = e.PayoutId,
            CreatedAt = e.CreatedAt
        }).ToList();

        return new EarningsSummaryDto
        {
            OrderCount = entries.Count,
            GrossRevenue = entries.Sum(e => e.OrderAmount),
            TotalCommission = entries.Sum(e => e.CommissionAmount),
            TotalTds = entries.Sum(e => e.TdsAmount),
            NetEarnings = entries.Sum(e => e.NetAmount),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            LedgerEntries = ledgerDtos
        };
    }
}
