using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Riders.Queries.GetProfile;

public sealed record GetRiderProfileQuery(Guid RiderId) : IRequest<Result<RiderProfileResponse>>;

public sealed record RiderProfileResponse(
    Guid Id,
    string Phone,
    string Name,
    string? Email,
    string VehicleType,
    string? VehicleNumber,
    string KycStatus,
    bool IsActive,
    bool IsOnline,
    decimal? CurrentLatitude,
    decimal? CurrentLongitude,
    string? BankAccountNumber,
    string? BankIfscCode,
    string? BankAccountName);