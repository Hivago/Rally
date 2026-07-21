using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Common;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Commands.ResetOwnerPassword;

internal sealed class ResetOwnerPasswordCommandHandler
    : IRequestHandler<ResetOwnerPasswordCommand, Result<ResetOwnerPasswordResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRestaurantOwnerRepository _ownerRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ResetOwnerPasswordCommandHandler(
        IAdminRepository adminRepository,
        IRestaurantOwnerRepository ownerRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _adminRepository = adminRepository;
        _ownerRepository = ownerRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ResetOwnerPasswordResponse>> Handle(
        ResetOwnerPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<ResetOwnerPasswordResponse>(
                Error.NotFound("Admin", request.RequestedByAdminId));

        if (admin.Role == AdminRole.Support)
            return Result.Failure<ResetOwnerPasswordResponse>(
                Error.Forbidden("Support role cannot reset owner passwords."));

        var owner = await _ownerRepository.GetByIdAsync(request.OwnerId, cancellationToken);
        if (owner is null)
            return Result.Failure<ResetOwnerPasswordResponse>(
                Error.NotFound("RestaurantOwner", request.OwnerId));

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var newHash = _passwordHasher.Hash(temporaryPassword);

        var updateResult = owner.UpdatePassword(newHash);
        if (updateResult.IsFailure)
            return Result.Failure<ResetOwnerPasswordResponse>(updateResult.Error);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ResetOwnerPasswordResponse(owner.Id, temporaryPassword));
    }
}
