using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetTdsSummary;

public sealed class GetTdsSummaryQueryHandler
    : IRequestHandler<GetTdsSummaryQuery, Result<TdsSummaryDto>>
{
    private static readonly TimeZoneInfo IstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly IPayoutLedgerRepository _ledgerRepository;

    public GetTdsSummaryQueryHandler(IPayoutLedgerRepository ledgerRepository)
    {
        _ledgerRepository = ledgerRepository;
    }

    public async Task<Result<TdsSummaryDto>> Handle(
        GetTdsSummaryQuery query,
        CancellationToken cancellationToken)
    {
        if (query.FromDate > query.ToDate)
            return Result.Failure<TdsSummaryDto>(Error.Validation("From date must be before or equal to To date."));

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(
            query.FromDate.ToDateTime(TimeOnly.MinValue), IstTimeZone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(
            query.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue), IstTimeZone);

        var entries = await _ledgerRepository.GetByOwnerIdAndDateRangeAsync(
            query.OwnerId, fromUtc, toUtc, cancellationToken);

        var lineItems = entries.Select(e => new TdsLineItemDto
        {
            OrderId = e.OrderId,
            OutletId = e.OutletId,
            OrderAmount = e.OrderAmount,
            CommissionAmount = e.CommissionAmount,
            TdsAmount = e.TdsAmount,
            NetAmount = e.NetAmount,
            CreatedAt = e.CreatedAt
        }).ToList();

        return new TdsSummaryDto
        {
            FromDate = query.FromDate,
            ToDate = query.ToDate,
            OrderCount = entries.Count,
            GrossOrderAmount = entries.Sum(e => e.OrderAmount),
            TotalCommission = entries.Sum(e => e.CommissionAmount),
            TotalTdsDeducted = entries.Sum(e => e.TdsAmount),
            NetAfterTds = entries.Sum(e => e.NetAmount),
            LineItems = lineItems
        };
    }
}
