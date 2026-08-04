using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Commands.SendOtp;

public sealed record SendRestaurantOtpCommand(string PhoneNumber) : IRequest<Result>;
