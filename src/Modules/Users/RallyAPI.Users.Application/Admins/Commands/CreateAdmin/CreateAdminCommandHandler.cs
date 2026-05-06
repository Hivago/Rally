using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Admins.Commands.CreateAdmin;

internal sealed class CreateAdminCommandHandler
    : IRequestHandler<CreateAdminCommand, Result<Guid>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAdminCommandHandler(
        IAdminRepository adminRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _adminRepository = adminRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        CreateAdminCommand request,
        CancellationToken cancellationToken)
    {
        // Verify requesting admin is SuperAdmin
        var requestingAdmin = await _adminRepository.GetByIdAsync(
            request.RequestedByAdminId,
            cancellationToken);

        if (requestingAdmin is null)
            return Result.Failure<Guid>(Error.NotFound("Admin", request.RequestedByAdminId));

        if (requestingAdmin.Role != AdminRole.SuperAdmin)
            return Result.Failure<Guid>(Error.Forbidden("Only SuperAdmin can create admins."));

        if (request.Role == AdminRole.SuperAdmin)
            return Result.Failure<Guid>(
                Error.Forbidden("SuperAdmin accounts cannot be created via the admin panel."));

        // Validate email
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<Guid>(emailResult.Error);

        // Check if email already exists
        var exists = await _adminRepository.ExistsByEmailAsync(emailResult.Value, cancellationToken);
        if (exists)
            return Result.Failure<Guid>(Error.Conflict("Admin with this email already exists."));

        // Hash password
        var passwordHash = _passwordHasher.Hash(request.Password);

        // Create admin
        var adminResult = Admin.Create(
            emailResult.Value,
            passwordHash,
            request.Name,
            request.Role);

        if (adminResult.IsFailure)
            return Result.Failure<Guid>(adminResult.Error);

        await _adminRepository.AddAsync(adminResult.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(adminResult.Value.Id);
    }
}