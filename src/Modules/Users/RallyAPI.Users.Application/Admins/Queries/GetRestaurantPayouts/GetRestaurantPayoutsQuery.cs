using MediatR;
using RallyAPI.SharedKernel.Abstractions.Payouts;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetRestaurantPayouts;

public sealed record GetRestaurantPayoutsQuery(
    Guid RequestedByAdminId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? OwnerId,
    string? Status,
    int Page,
    int PageSize) : IRequest<Result<RestaurantPayoutsPagedResult>>;
