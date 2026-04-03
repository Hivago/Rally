using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetPendingPayouts;

public sealed class GetPendingPayoutsQueryHandler
    : IRequestHandler<GetPendingPayoutsQuery, Result<IReadOnlyList<PayoutDto>>>
{
    private readonly IPayoutRepository _payoutRepository;

    public GetPendingPayoutsQueryHandler(IPayoutRepository payoutRepository)
    {
        _payoutRepository = payoutRepository;
    }

    public async Task<Result<IReadOnlyList<PayoutDto>>> Handle(
        GetPendingPayoutsQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;
        var payouts = await _payoutRepository.GetByStatusAsync(
            PayoutStatus.Pending, skip, query.PageSize, cancellationToken);

        var dtos = payouts.Select(p => new PayoutDto
        {
            Id = p.Id,
            OwnerId = p.OwnerId,
            PeriodStart = p.PeriodStart,
            PeriodEnd = p.PeriodEnd,
            OrderCount = p.OrderCount,
            GrossOrderAmount = p.GrossOrderAmount,
            TotalGstCollected = p.TotalGstCollected,
            TotalCommission = p.TotalCommission,
            TotalCommissionGst = p.TotalCommissionGst,
            TotalTds = p.TotalTds,
            NetPayoutAmount = p.NetPayoutAmount,
            Status = p.Status,
            TransactionReference = p.TransactionReference,
            PaidAt = p.PaidAt,
            Notes = p.Notes,
            CreatedAt = p.CreatedAt
        }).ToList();

        return Result.Success<IReadOnlyList<PayoutDto>>(dtos);
    }
}
