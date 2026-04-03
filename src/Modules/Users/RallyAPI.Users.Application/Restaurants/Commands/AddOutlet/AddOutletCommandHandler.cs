using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Restaurants.Commands.AddOutlet;

internal sealed class AddOutletCommandHandler
    : IRequestHandler<AddOutletCommand, Result<Guid>>
{
    private readonly IRestaurantOwnerRepository _ownerRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public AddOutletCommandHandler(
        IRestaurantOwnerRepository ownerRepository,
        IRestaurantRepository restaurantRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _ownerRepository = ownerRepository;
        _restaurantRepository = restaurantRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        AddOutletCommand request,
        CancellationToken cancellationToken)
    {
        // Verify owner exists and is active
        var owner = await _ownerRepository.GetByIdAsync(request.OwnerId, cancellationToken);
        if (owner is null)
            return Result.Failure<Guid>(Error.NotFound("RestaurantOwner", request.OwnerId));

        if (!owner.IsActive)
            return Result.Failure<Guid>(Error.Validation("Restaurant owner account is inactive."));

        // Validate phone & email
        var phoneResult = PhoneNumber.Create(request.Phone);
        if (phoneResult.IsFailure)
            return Result.Failure<Guid>(phoneResult.Error);

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<Guid>(emailResult.Error);

        // Check if email already taken
        var exists = await _restaurantRepository.ExistsByEmailAsync(emailResult.Value, cancellationToken);
        if (exists)
            return Result.Failure<Guid>(Error.Conflict("A restaurant with this email already exists."));

        // Hash password
        var passwordHash = _passwordHasher.Hash(request.Password);

        // Create restaurant outlet
        var restaurantResult = Restaurant.Create(
            request.Name,
            phoneResult.Value,
            emailResult.Value,
            passwordHash,
            request.AddressLine,
            request.Latitude,
            request.Longitude);

        if (restaurantResult.IsFailure)
            return Result.Failure<Guid>(restaurantResult.Error);

        var restaurant = restaurantResult.Value;

        // Link to owner
        var setOwnerResult = restaurant.SetOwner(request.OwnerId);
        if (setOwnerResult.IsFailure)
            return Result.Failure<Guid>(setOwnerResult.Error);

        // Set FSSAI if provided
        if (!string.IsNullOrEmpty(request.FssaiNumber))
        {
            var fssaiResult = restaurant.SetFssaiNumber(request.FssaiNumber);
            if (fssaiResult.IsFailure)
                return Result.Failure<Guid>(fssaiResult.Error);
        }

        await _restaurantRepository.AddAsync(restaurant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(restaurant.Id);
    }
}
