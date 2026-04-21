using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateDietary;

public sealed record UpdateRestaurantDietaryCommand(
    Guid RestaurantId,
    DietaryType? DietaryType,
    bool? IsVeganFriendly,
    bool? HasJainOptions,
    List<string>? CuisineTypes) : IRequest<Result>;
