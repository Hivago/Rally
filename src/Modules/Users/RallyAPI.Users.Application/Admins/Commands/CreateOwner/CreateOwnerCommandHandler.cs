using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Admins.Commands.CreateOwner;

internal sealed class CreateOwnerCommandHandler
    : IRequestHandler<CreateOwnerCommand, Result<CreateOwnerResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRestaurantOwnerRepository _ownerRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOwnerCommandHandler(
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

    public async Task<Result<CreateOwnerResponse>> Handle(
        CreateOwnerCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<CreateOwnerResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        if (admin.Role == AdminRole.Support)
            return Result.Failure<CreateOwnerResponse>(
                Error.Forbidden("Support role cannot create restaurant owners."));

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<CreateOwnerResponse>(emailResult.Error);

        var phoneResult = PhoneNumber.Create(request.Phone);
        if (phoneResult.IsFailure)
            return Result.Failure<CreateOwnerResponse>(phoneResult.Error);

        var emailExists = await _ownerRepository.ExistsByEmailAsync(emailResult.Value, cancellationToken);
        if (emailExists)
            return Result.Failure<CreateOwnerResponse>(
                Error.Conflict("Restaurant owner with this email already exists."));

        var passwordHash = _passwordHasher.Hash(request.Password);

        var ownerResult = RestaurantOwner.Create(
            request.Name,
            emailResult.Value,
            passwordHash,
            phoneResult.Value);

        if (ownerResult.IsFailure)
            return Result.Failure<CreateOwnerResponse>(ownerResult.Error);

        var owner = ownerResult.Value;

        if (!string.IsNullOrWhiteSpace(request.PanNumber))
        {
            var panResult = owner.SetPanNumber(request.PanNumber);
            if (panResult.IsFailure)
                return Result.Failure<CreateOwnerResponse>(panResult.Error);
        }

        if (!string.IsNullOrWhiteSpace(request.GstNumber))
        {
            var gstResult = owner.SetGstNumber(request.GstNumber);
            if (gstResult.IsFailure)
                return Result.Failure<CreateOwnerResponse>(gstResult.Error);
        }

        if (!string.IsNullOrWhiteSpace(request.BankAccountNumber)
            && !string.IsNullOrWhiteSpace(request.BankIfscCode)
            && !string.IsNullOrWhiteSpace(request.BankAccountName))
        {
            var bankResult = owner.UpdateBankDetails(
                request.BankAccountNumber,
                request.BankIfscCode,
                request.BankAccountName);

            if (bankResult.IsFailure)
                return Result.Failure<CreateOwnerResponse>(bankResult.Error);
        }

        await _ownerRepository.AddAsync(owner, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateOwnerResponse(owner.Id));
    }
}
