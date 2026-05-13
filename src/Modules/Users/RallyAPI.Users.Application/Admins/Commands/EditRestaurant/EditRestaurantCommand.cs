using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.EditRestaurant;

public sealed record EditRestaurantCommand(
    Guid RestaurantId,
    string? Name,
    string? Phone,
    string? AddressLine,
    // decimal? CommissionPercentage,   // Deprecated: percentage commission no longer supported. Only flat fee is used.
    decimal? CommissionFlatFee,
    int? AvgPrepTimeMins,
    List<string>? CuisineTypes,
    bool? IsPureVeg,
    bool? IsVeganFriendly,
    bool? HasJainOptions,
    decimal? MinOrderAmount,
    string? FssaiNumber,
    bool? AcceptsPickup) : IRequest<Result>;
