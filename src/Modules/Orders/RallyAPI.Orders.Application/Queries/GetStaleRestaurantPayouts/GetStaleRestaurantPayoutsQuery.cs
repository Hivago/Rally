using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.GetStaleRestaurantPayouts;

/// <summary>
/// Payouts stuck Processing (exported, no reconcile applied yet) for longer than
/// <paramref name="OlderThanDays"/>. Nothing currently pages anyone about this — it's a
/// pull report an admin should check regularly, not a push alert.
/// </summary>
public sealed record GetStaleRestaurantPayoutsQuery(int OlderThanDays = 3)
    : IRequest<Result<IReadOnlyList<StaleRestaurantPayoutDto>>>;

public sealed record StaleRestaurantPayoutDto(
    Guid PayoutId,
    Guid OwnerId,
    decimal NetPayoutAmount,
    Guid? ExportBatchId,
    DateTime? ExportedAtUtc,
    int DaysStale,
    string? BankAccountNumber,
    string? BankIfscCode);
