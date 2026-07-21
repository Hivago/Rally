using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Common;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Commands.ResetRestaurantPassword;

internal sealed class ResetRestaurantPasswordCommandHandler
    : IRequestHandler<ResetRestaurantPasswordCommand, Result<ResetRestaurantPasswordResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ResetRestaurantPasswordCommandHandler(
        IAdminRepository adminRepository,
        IRestaurantRepository restaurantRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _adminRepository = adminRepository;
        _restaurantRepository = restaurantRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ResetRestaurantPasswordResponse>> Handle(
        ResetRestaurantPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<ResetRestaurantPasswordResponse>(
                Error.NotFound("Admin", request.RequestedByAdminId));

        if (admin.Role == AdminRole.Support)
            return Result.Failure<ResetRestaurantPasswordResponse>(
                Error.Forbidden("Support role cannot reset restaurant passwords."));

        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure<ResetRestaurantPasswordResponse>(
                Error.NotFound("Restaurant", request.RestaurantId));

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var newHash = _passwordHasher.Hash(temporaryPassword);

        var updateResult = restaurant.UpdatePassword(newHash);
        if (updateResult.IsFailure)
            return Result.Failure<ResetRestaurantPasswordResponse>(updateResult.Error);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ResetRestaurantPasswordResponse(restaurant.Id, temporaryPassword));
    }
}
