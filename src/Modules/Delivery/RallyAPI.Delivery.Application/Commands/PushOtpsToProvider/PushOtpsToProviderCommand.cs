using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.PushOtpsToProvider;

/// <summary>
/// Recovery action — pushes the existing pickup/drop OTPs to the 3PL provider
/// and marks the task ready, transitioning it from UnFulfilled to Searching-for-Agent.
/// Use this to unstick tasks created before partner/order/update was wired into
/// the dispatch flow.
/// </summary>
public sealed record PushOtpsToProviderCommand(
    Guid OrderId,
    Guid CallerId,
    bool IsAdmin) : IRequest<Result<PushOtpsToProviderResult>>;

public sealed record PushOtpsToProviderResult(
    Guid DeliveryRequestId,
    string? ExternalTaskId,
    string? PickupCode,
    string? DropCode,
    string ProviderState);
