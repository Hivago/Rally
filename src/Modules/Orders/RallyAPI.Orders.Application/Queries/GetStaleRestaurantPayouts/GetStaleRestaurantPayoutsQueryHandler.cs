using MediatR;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetStaleRestaurantPayouts;

public sealed class GetStaleRestaurantPayoutsQueryHandler
    : IRequestHandler<GetStaleRestaurantPayoutsQuery, Result<IReadOnlyList<StaleRestaurantPayoutDto>>>
{
    private readonly IPayoutRepository _payoutRepository;

    public GetStaleRestaurantPayoutsQueryHandler(IPayoutRepository payoutRepository)
    {
        _payoutRepository = payoutRepository;
    }

    public async Task<Result<IReadOnlyList<StaleRestaurantPayoutDto>>> Handle(
        GetStaleRestaurantPayoutsQuery request,
        CancellationToken ct)
    {
        var olderThanDays = Math.Max(request.OlderThanDays, 0);
        var cutoffUtc = DateTime.UtcNow.AddDays(-olderThanDays);

        var stale = await _payoutRepository.GetStaleProcessingAsync(cutoffUtc, ct);

        var now = DateTime.UtcNow;
        var dtos = stale.Select(p => new StaleRestaurantPayoutDto(
            p.Id,
            p.OwnerId,
            p.NetPayoutAmount,
            p.ExportBatchId,
            p.ExportedAtUtc,
            p.ExportedAtUtc is { } exportedAt ? (int)(now - exportedAt).TotalDays : 0,
            p.BankAccountNumber,
            p.BankIfscCode)).ToList();

        return Result.Success<IReadOnlyList<StaleRestaurantPayoutDto>>(dtos);
    }
}
