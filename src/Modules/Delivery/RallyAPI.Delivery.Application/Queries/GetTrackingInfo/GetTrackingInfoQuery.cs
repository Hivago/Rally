using MediatR;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Queries.GetTrackingInfo;

public sealed record GetTrackingInfoQuery : IRequest<Result<TrackingDto>>
{
    public required string OrderNumber { get; init; }
}