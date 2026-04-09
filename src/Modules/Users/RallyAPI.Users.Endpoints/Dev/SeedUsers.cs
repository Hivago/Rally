using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Endpoints.Dev;

public class SeedUsers : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dev/seed-users", HandleAsync)
            .WithName("SeedUsers")
            .WithTags("Dev")
            .AllowAnonymous();
    }

    public record SeedUsersRequest(
        SeedAdminRequest? Admin,
        SeedRestaurantRequest? Restaurant,
        SeedRiderRequest? Rider);

    public record SeedAdminRequest(
        string Email,
        string Password,
        string Name,
        string Role = "SuperAdmin");

    public record SeedRestaurantRequest(
        string Name,
        string Phone,
        string Email,
        string Password,
        string AddressLine,
        decimal Latitude,
        decimal Longitude);

    public record SeedRiderRequest(
        string Phone,
        string Name,
        string VehicleType,
        string? VehicleNumber);

    private static async Task<IResult> HandleAsync(
        SeedUsersRequest request,
        IHostEnvironment env,
        IAdminRepository adminRepository,
        IRestaurantRepository restaurantRepository,
        IRiderRepository riderRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment())
            return Results.NotFound();

        var response = new Dictionary<string, object>();

        // Seed Admin
        if (request.Admin is not null)
        {
            var emailResult = Email.Create(request.Admin.Email);
            if (emailResult.IsFailure)
                return Results.BadRequest(new { error = $"Admin email: {emailResult.Error.Message}" });

            var exists = await adminRepository.ExistsByEmailAsync(emailResult.Value, cancellationToken);
            if (exists)
            {
                response["admin"] = new { status = "already_exists", email = request.Admin.Email };
            }
            else
            {
                if (!Enum.TryParse<AdminRole>(request.Admin.Role, true, out var role))
                    return Results.BadRequest(new { error = "Invalid admin role. Use: Support, CityAdmin, SuperAdmin" });

                var hash = passwordHasher.Hash(request.Admin.Password);
                var adminResult = Admin.Create(emailResult.Value, hash, request.Admin.Name, role);
                if (adminResult.IsFailure)
                    return Results.BadRequest(new { error = $"Admin: {adminResult.Error.Message}" });

                await adminRepository.AddAsync(adminResult.Value, cancellationToken);
                response["admin"] = new
                {
                    status = "created",
                    id = adminResult.Value.Id,
                    email = request.Admin.Email,
                    password = request.Admin.Password,
                    role = request.Admin.Role,
                    loginEndpoint = "POST /api/admins/login"
                };
            }
        }

        // Seed Restaurant
        if (request.Restaurant is not null)
        {
            var emailResult = Email.Create(request.Restaurant.Email);
            if (emailResult.IsFailure)
                return Results.BadRequest(new { error = $"Restaurant email: {emailResult.Error.Message}" });

            var phoneResult = PhoneNumber.Create(request.Restaurant.Phone);
            if (phoneResult.IsFailure)
                return Results.BadRequest(new { error = $"Restaurant phone: {phoneResult.Error.Message}" });

            var exists = await restaurantRepository.ExistsByEmailAsync(emailResult.Value, cancellationToken);
            if (exists)
            {
                response["restaurant"] = new { status = "already_exists", email = request.Restaurant.Email };
            }
            else
            {
                var hash = passwordHasher.Hash(request.Restaurant.Password);
                var restResult = Restaurant.Create(
                    request.Restaurant.Name,
                    phoneResult.Value,
                    emailResult.Value,
                    hash,
                    request.Restaurant.AddressLine,
                    request.Restaurant.Latitude,
                    request.Restaurant.Longitude);

                if (restResult.IsFailure)
                    return Results.BadRequest(new { error = $"Restaurant: {restResult.Error.Message}" });

                await restaurantRepository.AddAsync(restResult.Value, cancellationToken);
                response["restaurant"] = new
                {
                    status = "created",
                    id = restResult.Value.Id,
                    email = request.Restaurant.Email,
                    password = request.Restaurant.Password,
                    phone = request.Restaurant.Phone,
                    loginEndpoint = "POST /api/restaurants/login"
                };
            }
        }

        // Seed Rider
        if (request.Rider is not null)
        {
            var phoneResult = PhoneNumber.Create(request.Rider.Phone);
            if (phoneResult.IsFailure)
                return Results.BadRequest(new { error = $"Rider phone: {phoneResult.Error.Message}" });

            if (!Enum.TryParse<VehicleType>(request.Rider.VehicleType, true, out var vehicleType))
                return Results.BadRequest(new { error = "Invalid vehicle type. Use: Bicycle, Bike, Scooter, Car, Auto" });

            var exists = await riderRepository.ExistsByPhoneAsync(phoneResult.Value, cancellationToken);
            if (exists)
            {
                response["rider"] = new { status = "already_exists", phone = request.Rider.Phone };
            }
            else
            {
                var riderResult = Rider.Create(phoneResult.Value, request.Rider.Name, vehicleType);
                if (riderResult.IsFailure)
                    return Results.BadRequest(new { error = $"Rider: {riderResult.Error.Message}" });

                var rider = riderResult.Value;
                if (!string.IsNullOrWhiteSpace(request.Rider.VehicleNumber))
                    rider.UpdateProfile(null, null, request.Rider.VehicleNumber);

                await riderRepository.AddAsync(rider, cancellationToken);
                response["rider"] = new
                {
                    status = "created",
                    id = rider.Id,
                    phone = request.Rider.Phone,
                    name = request.Rider.Name,
                    vehicleType = request.Rider.VehicleType,
                    kycStatus = "Pending",
                    loginMethod = "OTP via POST /api/riders/otp/send then POST /api/riders/otp/verify"
                };
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Seed complete", users = response });
    }
}
