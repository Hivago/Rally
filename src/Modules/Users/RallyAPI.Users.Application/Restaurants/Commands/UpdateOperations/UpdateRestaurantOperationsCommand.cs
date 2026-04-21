using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateOperations;

public sealed record UpdateRestaurantOperationsCommand(
    Guid RestaurantId,
    bool? AutoAcceptOrders,
    int? AvgPrepTimeMins,
    bool? IsAcceptingOrders,
    decimal? MinOrderAmount,
    bool? UseCustomSchedule) : IRequest<Result>;
