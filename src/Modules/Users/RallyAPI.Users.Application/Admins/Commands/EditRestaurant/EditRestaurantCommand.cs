using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.EditRestaurant;

public sealed record EditRestaurantCommand(
    Guid RestaurantId,
    string? Name,
    string? Phone,
    string? AddressLine,
    decimal? CommissionPercentage,
    int? AvgPrepTimeMins,
    List<string>? CuisineTypes,
    bool? IsPureVeg,
    bool? IsVeganFriendly,
    bool? HasJainOptions,
    decimal? MinOrderAmount,
    string? FssaiNumber) : IRequest<Result>;
