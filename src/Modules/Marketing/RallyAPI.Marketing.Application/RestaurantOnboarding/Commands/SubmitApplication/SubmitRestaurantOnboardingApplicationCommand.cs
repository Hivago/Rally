using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.RestaurantOnboarding.Commands.SubmitApplication;

/// <summary>
/// Public onboarding-form submission. All fields below are PLAINTEXT as received from the
/// form — the handler is responsible for encrypting BankAccountNumber/PanNumber/GstNumber
/// before they're ever written to the entity or the database. Creates a Pending application
/// only; no live owner/restaurant account is created here.
/// </summary>
public sealed record SubmitRestaurantOnboardingApplicationCommand(
    string RestaurantName,
    string OwnerName,
    string Phone,
    string Email,
    string City,
    string AddressLine,
    string? CuisineType,
    string? FssaiNumber,
    string BankAccountNumber,
    string BankIfscCode,
    string BankAccountName,
    string PanNumber,
    string? GstNumber,
    string? Source,
    string? IpAddress) : IRequest<Result<SubmitRestaurantOnboardingApplicationResponse>>;

public sealed record SubmitRestaurantOnboardingApplicationResponse(Guid Id);
