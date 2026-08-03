//using MediatR;
//using RallyAPI.SharedKernel.Results;

//namespace RallyAPI.Users.Application.Restaurants.Commands.Login;

//public sealed record LoginRestaurantCommand(
//    string Email,
//    string Password) : IRequest<Result<LoginRestaurantResponse>>;

//public sealed record LoginRestaurantResponse(
//    Guid RestaurantId,
//    string Name,
//    string Token);

using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Commands.Login;

public sealed record LoginRestaurantCommand(
    string Email,
    string Password) : IRequest<Result<LoginRestaurantResponse>>;

public sealed record LoginRestaurantResponse(
    Guid RestaurantId,
    string Name,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    bool MustChangePassword);