using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetRiderPayouts;

public sealed record GetRiderPayoutsQuery(
    Guid RequestedByAdminId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? RiderId,
    string? Status,
    int Page,
    int PageSize) : IRequest<Result<RiderPayoutsPagedResult>>;
