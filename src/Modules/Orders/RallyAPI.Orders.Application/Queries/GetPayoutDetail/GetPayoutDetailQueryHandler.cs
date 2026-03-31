using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetPayoutDetail;

public sealed class GetPayoutDetailQueryHandler
    : IRequestHandler<GetPayoutDetailQuery, Result<PayoutDetailDto>>
{
    private readonly IPayoutRepository _payoutRepository;
    private readonly IPayoutLedgerRepository _ledgerRepository;

    public GetPayoutDetailQueryHandler(
        IPayoutRepository payoutRepository,
        IPayoutLedgerRepository ledgerRepository)
    {
        _payoutRepository = payoutRepository;
        _ledgerRepository = ledgerRepository;
    }

    public async Task<Result<PayoutDetailDto>> Handle(
        GetPayoutDetailQuery query,
        CancellationToken cancellationToken)
    {
        var payout = await _payoutRepository.GetByIdAsync(query.PayoutId, cancellationToken);

        if (payout is null)
            return Result.Failure<PayoutDetailDto>(Error.NotFound("Payout", query.PayoutId));

        if (payout.OwnerId != query.OwnerId)
            return Result.Failure<PayoutDetailDto>(Error.NotFound("Payout", query.PayoutId));

        var ledgerEntries = await _ledgerRepository.GetByPayoutIdAsync(
            query.PayoutId, cancellationToken);

        var ledgerDtos = ledgerEntries.Select(e => new PayoutLedgerDto
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

        return new PayoutDetailDto
        {
            Id = payout.Id,
            OwnerId = payout.OwnerId,
            PeriodStart = payout.PeriodStart,
            PeriodEnd = payout.PeriodEnd,
            OrderCount = payout.OrderCount,
            GrossOrderAmount = payout.GrossOrderAmount,
            TotalGstCollected = payout.TotalGstCollected,
            TotalCommission = payout.TotalCommission,
            TotalCommissionGst = payout.TotalCommissionGst,
            TotalTds = payout.TotalTds,
            NetPayoutAmount = payout.NetPayoutAmount,
            Status = payout.Status,
            TransactionReference = payout.TransactionReference,
            PaidAt = payout.PaidAt,
            Notes = payout.Notes,
            CreatedAt = payout.CreatedAt,
            LedgerEntries = ledgerDtos
        };
    }
}
