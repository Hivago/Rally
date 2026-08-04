using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetStaleRiderPayouts;

public sealed class GetStaleRiderPayoutsQueryHandler
    : IRequestHandler<GetStaleRiderPayoutsQuery, Result<IReadOnlyList<StaleRiderPayoutDto>>>
{
    private readonly IRiderPayoutLedgerRepository _ledgerRepository;

    public GetStaleRiderPayoutsQueryHandler(IRiderPayoutLedgerRepository ledgerRepository)
    {
        _ledgerRepository = ledgerRepository;
    }

    public async Task<Result<IReadOnlyList<StaleRiderPayoutDto>>> Handle(
        GetStaleRiderPayoutsQuery request,
        CancellationToken ct)
    {
        var olderThanDays = Math.Max(request.OlderThanDays, 0);
        var cutoffUtc = DateTime.UtcNow.AddDays(-olderThanDays);

        var stale = await _ledgerRepository.GetStaleProcessingAsync(cutoffUtc, ct);

        var now = DateTime.UtcNow;
        var dtos = stale.Select(p => new StaleRiderPayoutDto(
            p.Id,
            p.RiderId,
            p.NetPayable,
            p.ExportBatchId,
            p.ExportedAtUtc,
            p.ExportedAtUtc is { } exportedAt ? (int)(now - exportedAt).TotalDays : 0)).ToList();

        return Result.Success<IReadOnlyList<StaleRiderPayoutDto>>(dtos);
    }
}
