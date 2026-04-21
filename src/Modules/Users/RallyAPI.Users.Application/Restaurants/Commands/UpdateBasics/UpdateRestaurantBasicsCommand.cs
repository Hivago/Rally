using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateBasics;

public sealed record UpdateRestaurantBasicsCommand(
    Guid RestaurantId,
    string? Name,
    string? Phone,
    string? Email,
    string? FssaiNumber,
    string? AddressLine,
    decimal? Latitude,
    decimal? Longitude,
    string? Description) : IRequest<Result>;
