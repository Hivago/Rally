using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Owners.Queries.GetOutlets;

namespace RallyAPI.Users.Application.Restaurants.Queries.GetMyOutlets;

public sealed class GetMyOutletsQueryHandler
    : IRequestHandler<GetMyOutletsQuery, Result<IReadOnlyList<OutletSummaryResponse>>>
{
    private readonly IRestaurantRepository _restaurantRepository;

    public GetMyOutletsQueryHandler(IRestaurantRepository restaurantRepository)
    {
        _restaurantRepository = restaurantRepository;
    }

    public async Task<Result<IReadOnlyList<OutletSummaryResponse>>> Handle(
        GetMyOutletsQuery request,
        CancellationToken cancellationToken)
    {
        var outlets = await _restaurantRepository.GetByIdsAsync(request.RestaurantIds, cancellationToken);

        var response = outlets
            .Select(r => new OutletSummaryResponse(
                r.Id,
                r.Name,
                r.Email.Value,
                r.AddressLine,
                r.IsActive,
                r.IsAcceptingOrders,
                r.LogoUrl))
            .ToList();

        return Result.Success<IReadOnlyList<OutletSummaryResponse>>(response);
    }
}
