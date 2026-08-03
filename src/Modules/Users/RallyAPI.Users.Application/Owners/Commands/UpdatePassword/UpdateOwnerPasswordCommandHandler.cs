using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Owners.Commands.UpdatePassword;

internal sealed class UpdateOwnerPasswordCommandHandler
    : IRequestHandler<UpdateOwnerPasswordCommand, Result>
{
    private readonly IRestaurantOwnerRepository _ownerRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOwnerPasswordCommandHandler(
        IRestaurantOwnerRepository ownerRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _ownerRepository = ownerRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateOwnerPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var owner = await _ownerRepository.GetByIdAsync(request.OwnerId, cancellationToken);
        if (owner is null)
            return Result.Failure(Error.NotFound("RestaurantOwner", request.OwnerId));

        if (!_passwordHasher.Verify(request.CurrentPassword, owner.PasswordHash))
            return Result.Failure(Error.Validation("Current password is incorrect."));

        var newHash = _passwordHasher.Hash(request.NewPassword);
        var result = owner.UpdatePassword(newHash);
        if (result.IsFailure)
            return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
