using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetGstSummary;

public sealed class GetGstSummaryQueryHandler
    : IRequestHandler<GetGstSummaryQuery, Result<GstSummaryDto>>
{
    private static readonly TimeZoneInfo IstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly IPayoutLedgerRepository _ledgerRepository;

    public GetGstSummaryQueryHandler(IPayoutLedgerRepository ledgerRepository)
    {
        _ledgerRepository = ledgerRepository;
    }

    public async Task<Result<GstSummaryDto>> Handle(
        GetGstSummaryQuery query,
        CancellationToken cancellationToken)
    {
        if (query.FromDate > query.ToDate)
            return Result.Failure<GstSummaryDto>(Error.Validation("From date must be before or equal to To date."));

        // Convert IST date range to UTC for querying
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(
            query.FromDate.ToDateTime(TimeOnly.MinValue), IstTimeZone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(
            query.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue), IstTimeZone);

        var entries = await _ledgerRepository.GetByOwnerIdAndDateRangeAsync(
            query.OwnerId, fromUtc, toUtc, cancellationToken);

        var lineItems = entries.Select(e => new GstLineItemDto
        {
            OrderId = e.OrderId,
            OutletId = e.OutletId,
            OrderAmount = e.OrderAmount,
            GstAmount = e.GstAmount,
            CommissionAmount = e.CommissionAmount,
            CommissionGst = e.CommissionGst,
            CreatedAt = e.CreatedAt
        }).ToList();

        return new GstSummaryDto
        {
            FromDate = query.FromDate,
            ToDate = query.ToDate,
            OrderCount = entries.Count,
            GrossOrderAmount = entries.Sum(e => e.OrderAmount),
            TotalGstOnOrders = entries.Sum(e => e.GstAmount),
            TotalCommission = entries.Sum(e => e.CommissionAmount),
            TotalCommissionGst = entries.Sum(e => e.CommissionGst),
            LineItems = lineItems
        };
    }
}
