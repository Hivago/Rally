using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.Waitlist.Commands.JoinWaitlist;

public sealed record JoinWaitlistCommand(
    string Name,
    string Email,
    string Phone,
    string? Source,
    string? IpAddress) : IRequest<Result<JoinWaitlistResponse>>;

public sealed record JoinWaitlistResponse(Guid Id);
