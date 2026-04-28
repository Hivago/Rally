using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Admins.Commands.CreateAdminPanelRestaurant;

internal sealed class CreateAdminPanelRestaurantCommandHandler
    : IRequestHandler<CreateAdminPanelRestaurantCommand, Result<CreateAdminPanelRestaurantResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantOwnerRepository _ownerRepository;
    private readonly IRestaurantCodeGenerator _codeGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAdminPanelRestaurantCommandHandler(
        IAdminRepository adminRepository,
        IRestaurantRepository restaurantRepository,
        IRestaurantOwnerRepository ownerRepository,
        IRestaurantCodeGenerator codeGenerator,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _adminRepository = adminRepository;
        _restaurantRepository = restaurantRepository;
        _ownerRepository = ownerRepository;
        _codeGenerator = codeGenerator;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateAdminPanelRestaurantResponse>> Handle(
        CreateAdminPanelRestaurantCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<CreateAdminPanelRestaurantResponse>(
                Error.NotFound("Admin", request.RequestedByAdminId));

        if (admin.Role == AdminRole.Support)
            return Result.Failure<CreateAdminPanelRestaurantResponse>(
                Error.Forbidden("Support role cannot create restaurants."));

        var owner = await _ownerRepository.GetByIdAsync(request.OwnerId, cancellationToken);
        if (owner is null)
            return Result.Failure<CreateAdminPanelRestaurantResponse>(
                Error.NotFound("RestaurantOwner", request.OwnerId));

        if (!owner.IsActive)
            return Result.Failure<CreateAdminPanelRestaurantResponse>(
                Error.Validation("Owner is inactive. Reactivate the owner before adding outlets."));

        var phoneResult = PhoneNumber.Create(request.Phone);
        if (phoneResult.IsFailure)
            return Result.Failure<CreateAdminPanelRestaurantResponse>(phoneResult.Error);

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<CreateAdminPanelRestaurantResponse>(emailResult.Error);

        var emailExists = await _restaurantRepository.ExistsByEmailAsync(emailResult.Value, cancellationToken);
        if (emailExists)
            return Result.Failure<CreateAdminPanelRestaurantResponse>(
                Error.Conflict("Restaurant with this email already exists."));

        var passwordHash = _passwordHasher.Hash(request.Password);
        var rstCode = await _codeGenerator.NextAsync(cancellationToken);

        var restaurantResult = Restaurant.Create(
            request.Name,
            phoneResult.Value,
            emailResult.Value,
            passwordHash,
            request.AddressLine,
            request.Latitude,
            request.Longitude,
            rstCode);

        if (restaurantResult.IsFailure)
            return Result.Failure<CreateAdminPanelRestaurantResponse>(restaurantResult.Error);

        var ownerLinkResult = restaurantResult.Value.SetOwner(request.OwnerId);
        if (ownerLinkResult.IsFailure)
            return Result.Failure<CreateAdminPanelRestaurantResponse>(ownerLinkResult.Error);

        await _restaurantRepository.AddAsync(restaurantResult.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateAdminPanelRestaurantResponse(
            restaurantResult.Value.Id,
            rstCode));
    }
}
