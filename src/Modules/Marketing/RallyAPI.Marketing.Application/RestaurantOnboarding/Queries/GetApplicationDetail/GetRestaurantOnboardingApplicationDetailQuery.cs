using MediatR;
using RallyAPI.Marketing.Domain.Enums;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.RestaurantOnboarding.Queries.GetApplicationDetail;

/// <summary>
/// <paramref name="IncludeSensitiveDetails"/> must be computed by the ENDPOINT after checking
/// the caller is Super Admin (AdminRole.SuperAdmin) — this query does not know about admin
/// roles (module boundary: Marketing.Application cannot reference Users.Domain). When false,
/// decryption never happens at all; only the masked fields are populated.
/// </summary>
public sealed record GetRestaurantOnboardingApplicationDetailQuery(
    Guid ApplicationId,
    bool IncludeSensitiveDetails) : IRequest<Result<RestaurantOnboardingApplicationDetailDto?>>;

public sealed record RestaurantOnboardingApplicationDetailDto(
    Guid Id,
    string RestaurantName,
    string OwnerName,
    string Phone,
    string Email,
    string City,
    string AddressLine,
    string? CuisineType,
    string? FssaiNumber,
    string BankAccountMasked,
    string? BankAccountNumber,   // null unless IncludeSensitiveDetails
    string BankIfscCode,
    string BankAccountName,
    string PanMasked,
    string? PanNumber,           // null unless IncludeSensitiveDetails
    string? GstMasked,
    string? GstNumber,           // null unless IncludeSensitiveDetails
    OnboardingApplicationStatus Status,
    Guid? ReviewedByAdminId,
    DateTime? ReviewedAtUtc,
    string? ReviewNotes,
    string? Source,
    DateTime CreatedAt);
