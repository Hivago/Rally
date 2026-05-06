using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Commands.ChangePassword;

internal sealed class ChangeAdminPasswordCommandHandler
    : IRequestHandler<ChangeAdminPasswordCommand, Result>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeAdminPasswordCommandHandler(
        IAdminRepository adminRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _adminRepository = adminRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ChangeAdminPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.AdminId, cancellationToken);
        if (admin is null)
            return Result.Failure(Error.NotFound("Admin", request.AdminId));

        if (!_passwordHasher.Verify(request.CurrentPassword, admin.PasswordHash))
            return Result.Failure(Error.Validation("Current password is incorrect."));

        var newHash = _passwordHasher.Hash(request.NewPassword);

        var updateResult = admin.UpdatePassword(newHash);
        if (updateResult.IsFailure)
            return updateResult;

        _adminRepository.Update(admin);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
