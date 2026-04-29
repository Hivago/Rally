using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.AdminAssignRider;

/// <summary>
/// Admin manual rider assignment. Bypasses the dispatch retry loop and assigns the
/// chosen rider directly. Rider name/phone are looked up server-side via
/// IRiderQueryService — admin only supplies the riderId.
/// </summary>
public sealed record AdminAssignRiderCommand(
    Guid OrderId,
    Guid RiderId) : IRequest<Result>;
