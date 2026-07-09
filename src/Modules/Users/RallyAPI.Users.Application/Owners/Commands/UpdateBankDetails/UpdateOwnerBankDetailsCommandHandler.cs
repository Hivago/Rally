using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Owners.Commands.UpdateBankDetails;

internal sealed class UpdateOwnerBankDetailsCommandHandler
    : IRequestHandler<UpdateOwnerBankDetailsCommand, Result>
{
    private readonly IRestaurantOwnerRepository _ownerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOwnerBankDetailsCommandHandler(
        IRestaurantOwnerRepository ownerRepository,
        IUnitOfWork unitOfWork)
    {
        _ownerRepository = ownerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateOwnerBankDetailsCommand request,
        CancellationToken cancellationToken)
    {
        var owner = await _ownerRepository.GetByIdAsync(request.OwnerId, cancellationToken);
        if (owner is null)
            return Result.Failure(Error.NotFound("RestaurantOwner", request.OwnerId));

        var result = owner.UpdateBankDetails(
            request.BankAccountNumber,
            request.BankIfscCode,
            request.BankAccountName);

        if (result.IsFailure)
            return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
