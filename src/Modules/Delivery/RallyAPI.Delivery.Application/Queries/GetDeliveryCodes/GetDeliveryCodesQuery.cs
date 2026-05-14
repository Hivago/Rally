using MediatR;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Queries.GetDeliveryCodes;

public sealed record GetDeliveryCodesQuery(Guid OrderId, Guid CallerId, string CallerRole)
    : IRequest<Result<DeliveryCodesDto>>;
