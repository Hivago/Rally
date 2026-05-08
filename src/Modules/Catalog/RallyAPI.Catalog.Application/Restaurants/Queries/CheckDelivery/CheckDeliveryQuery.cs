using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.CheckDelivery;

public sealed record CheckDeliveryQuery(
    Guid RestaurantId,
    double CustomerLat,
    double CustomerLng
) : IRequest<Result<DeliveryCheckResponse>>;

public sealed record DeliveryCheckResponse(
    bool CanDeliver,
    double DistanceKm,
    double MaxDistanceKm);
