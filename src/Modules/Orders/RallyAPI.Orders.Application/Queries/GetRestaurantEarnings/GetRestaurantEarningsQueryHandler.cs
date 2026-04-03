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
        // Calculate current week (Monday-Sunday IST)
        var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstTimeZone);
        var daysFromMonday = ((int)istNow.DayOfWeek - 1 + 7) % 7;
        var mondayIst = istNow.Date.AddDays(-daysFromMonday);
        var sundayIst = mondayIst.AddDays(6);

        var periodStart = DateOnly.FromDateTime(mondayIst);
        var periodEnd = DateOnly.FromDateTime(sundayIst);

        // Get all pending entries for this owner (current week = not yet batched)
        var entries = await _ledgerRepository.GetPendingByOwnerIdAsync(
            query.OwnerId, cancellationToken);

        var ledgerDtos = entries.Select(e => new PayoutLedgerDto
        {
            Id = e.Id,
            OutletId = e.OutletId,
            OrderId = e.OrderId,
            OrderAmount = e.OrderAmount,
            GstAmount = e.GstAmount,
            CommissionPercentage = e.CommissionPercentage,
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
