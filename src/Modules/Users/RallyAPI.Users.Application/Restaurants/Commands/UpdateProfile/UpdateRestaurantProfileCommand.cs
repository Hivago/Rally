using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateProfile;

public sealed record UpdateRestaurantProfileCommand(
    Guid RestaurantId,
    string? Name,
    string? AddressLine,
    string? Phone,
    List<string>? CuisineTypes,
    bool? IsPureVeg,
    bool? IsVeganFriendly,
    bool? HasJainOptions,
    decimal? MinOrderAmount,
    string? FssaiNumber) : IRequest<Result>;
