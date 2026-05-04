using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.ChangePassword;

public sealed record ChangeAdminPasswordCommand(
    Guid AdminId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword) : IRequest<Result>;
