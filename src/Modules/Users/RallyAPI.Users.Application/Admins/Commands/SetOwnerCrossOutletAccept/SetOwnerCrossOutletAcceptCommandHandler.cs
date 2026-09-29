using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Commands.SetOwnerCrossOutletAccept;

internal sealed class SetOwnerCrossOutletAcceptCommandHandler
    : IRequestHandler<SetOwnerCrossOutletAcceptCommand, Result<SetOwnerCrossOutletAcceptResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRestaurantOwnerRepository _ownerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetOwnerCrossOutletAcceptCommandHandler(
        IAdminRepository adminRepository,
        IRestaurantOwnerRepository ownerRepository,
        IUnitOfWork unitOfWork)
    {
        _adminRepository = adminRepository;
        _ownerRepository = ownerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SetOwnerCrossOutletAcceptResponse>> Handle(
        SetOwnerCrossOutletAcceptCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<SetOwnerCrossOutletAcceptResponse>(
                Error.NotFound("Admin", request.RequestedByAdminId));

        if (admin.Role == AdminRole.Support)
            return Result.Failure<SetOwnerCrossOutletAcceptResponse>(
                Error.Forbidden("Support role cannot change cross-outlet accept settings."));

        var owner = await _ownerRepository.GetByIdAsync(request.OwnerId, cancellationToken);
        if (owner is null)
            return Result.Failure<SetOwnerCrossOutletAcceptResponse>(
                Error.NotFound("RestaurantOwner", request.OwnerId));

        // Idempotent PUT-style semantics: setting to the current value is a no-op success,
        // not a validation error (unlike Activate/Deactivate's toggle-style convention).
        if (owner.CrossOutletAcceptEnabled != request.Enabled)
        {
            var updateResult = owner.SetCrossOutletAcceptEnabled(request.Enabled);
            if (updateResult.IsFailure)
                return Result.Failure<SetOwnerCrossOutletAcceptResponse>(updateResult.Error);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new SetOwnerCrossOutletAcceptResponse(owner.Id, owner.CrossOutletAcceptEnabled));
    }
}
