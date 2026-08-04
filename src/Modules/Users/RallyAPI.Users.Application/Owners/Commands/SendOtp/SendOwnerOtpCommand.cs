using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Owners.Commands.SendOtp;

public sealed record SendOwnerOtpCommand(string PhoneNumber) : IRequest<Result>;
