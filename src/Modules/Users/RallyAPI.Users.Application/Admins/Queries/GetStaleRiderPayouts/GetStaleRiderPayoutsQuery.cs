using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetStaleRiderPayouts;

/// <summary>
/// Rider payouts stuck Processing longer than <paramref name="OlderThanDays"/>. Mirrors
/// GetStaleRestaurantPayoutsQuery (Orders module).
/// </summary>
public sealed record GetStaleRiderPayoutsQuery(int OlderThanDays = 3)
    : IRequest<Result<IReadOnlyList<StaleRiderPayoutDto>>>;

public sealed record StaleRiderPayoutDto(
    Guid PayoutId,
    Guid RiderId,
    decimal NetPayable,
    Guid? ExportBatchId,
    DateTime? ExportedAtUtc,
    int DaysStale);
