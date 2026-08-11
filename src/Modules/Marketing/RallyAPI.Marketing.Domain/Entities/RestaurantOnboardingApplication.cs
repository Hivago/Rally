using RallyAPI.Marketing.Domain.Enums;
using RallyAPI.SharedKernel.Domain;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Domain.Entities;

/// <summary>
/// A restaurant's self-submitted request to join the platform, captured via the public
/// onboarding form. Deliberately NOT a live owner/restaurant account — approving an
/// application is a manual admin decision, and even after approval, creating the real
/// account (with real login credentials) is a separate follow-up step. This entity only
/// exists to hold submitted-but-unverified data safely until a human reviews it.
///
/// Bank account number, PAN, and GST are encrypted by the caller (the command handler, using
/// IFieldEncryptionService from SharedKernel) BEFORE reaching this entity — the *Encrypted
/// properties always hold ciphertext, never plaintext. This entity intentionally has no
/// knowledge of encryption; format validation (PAN/GST/account-number shape) happens on the
/// plaintext values in the command validator, before they're encrypted and handed here.
/// </summary>
public sealed class RestaurantOnboardingApplication : BaseEntity
{
    public string RestaurantName { get; private set; } = string.Empty;
    public string OwnerName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string AddressLine { get; private set; } = string.Empty;
    public string? CuisineType { get; private set; }
    public string? FssaiNumber { get; private set; }

    // Temporarily optional — onboarding.hivago.in doesn't collect bank details yet. Revert to
    // required (non-nullable, with the Create() guard clauses below) once the form catches up.
    public string? BankAccountNumberEncrypted { get; private set; }
    /// <summary>Last 4 digits, stored in plaintext so the admin list view can show a masked hint without decrypting every row.</summary>
    public string? BankAccountLast4 { get; private set; }
    public string? BankIfscCode { get; private set; }
    public string? BankAccountName { get; private set; }

    public string PanNumberEncrypted { get; private set; } = string.Empty;
    public string PanLast4 { get; private set; } = string.Empty;

    public string? GstNumberEncrypted { get; private set; }
    public string? GstLast4 { get; private set; }

    public OnboardingApplicationStatus Status { get; private set; }
    public Guid? ReviewedByAdminId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewNotes { get; private set; }

    public string? Source { get; private set; }
    public string? IpAddress { get; private set; }

    private RestaurantOnboardingApplication() { }

    public static Result<RestaurantOnboardingApplication> Create(
        string restaurantName,
        string ownerName,
        string phone,
        string email,
        string city,
        string addressLine,
        string? cuisineType,
        string? fssaiNumber,
        string? bankAccountNumberEncrypted,
        string? bankAccountLast4,
        string? bankIfscCode,
        string? bankAccountName,
        string panNumberEncrypted,
        string panLast4,
        string? gstNumberEncrypted,
        string? gstLast4,
        string? source,
        string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(restaurantName))
            return Result.Failure<RestaurantOnboardingApplication>(Error.Validation("Restaurant name is required."));

        if (string.IsNullOrWhiteSpace(ownerName))
            return Result.Failure<RestaurantOnboardingApplication>(Error.Validation("Owner name is required."));

        if (string.IsNullOrWhiteSpace(phone))
            return Result.Failure<RestaurantOnboardingApplication>(Error.Validation("Phone is required."));

        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<RestaurantOnboardingApplication>(Error.Validation("Email is required."));

        if (string.IsNullOrWhiteSpace(city))
            return Result.Failure<RestaurantOnboardingApplication>(Error.Validation("City is required."));

        if (string.IsNullOrWhiteSpace(addressLine))
            return Result.Failure<RestaurantOnboardingApplication>(Error.Validation("Address is required."));

        // Bank details are temporarily optional — see the property comments above.

        if (string.IsNullOrWhiteSpace(panNumberEncrypted))
            return Result.Failure<RestaurantOnboardingApplication>(Error.Validation("PAN number is required."));

        return Result.Success(new RestaurantOnboardingApplication
        {
            Id = Guid.NewGuid(),
            RestaurantName = restaurantName.Trim(),
            OwnerName = ownerName.Trim(),
            Phone = phone.Trim(),
            Email = email.Trim(),
            City = city.Trim(),
            AddressLine = addressLine.Trim(),
            CuisineType = cuisineType?.Trim(),
            FssaiNumber = fssaiNumber?.Trim(),
            BankAccountNumberEncrypted = bankAccountNumberEncrypted,
            BankAccountLast4 = bankAccountLast4,
            BankIfscCode = bankIfscCode?.Trim().ToUpperInvariant(),
            BankAccountName = bankAccountName?.Trim(),
            PanNumberEncrypted = panNumberEncrypted,
            PanLast4 = panLast4,
            GstNumberEncrypted = gstNumberEncrypted,
            GstLast4 = gstLast4,
            Status = OnboardingApplicationStatus.Pending,
            Source = source?.Trim(),
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public Result Approve(Guid reviewedByAdminId, string? notes)
    {
        if (Status != OnboardingApplicationStatus.Pending)
            return Result.Failure(Error.Conflict($"Cannot approve an application in {Status} status."));

        if (reviewedByAdminId == Guid.Empty)
            return Result.Failure(Error.Validation("Reviewing admin ID is required."));

        Status = OnboardingApplicationStatus.Approved;
        ReviewedByAdminId = reviewedByAdminId;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNotes = notes?.Trim();
        MarkAsUpdated();
        return Result.Success();
    }

    public Result Reject(Guid reviewedByAdminId, string reason)
    {
        if (Status != OnboardingApplicationStatus.Pending)
            return Result.Failure(Error.Conflict($"Cannot reject an application in {Status} status."));

        if (reviewedByAdminId == Guid.Empty)
            return Result.Failure(Error.Validation("Reviewing admin ID is required."));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("A rejection reason is required."));

        Status = OnboardingApplicationStatus.Rejected;
        ReviewedByAdminId = reviewedByAdminId;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNotes = reason.Trim();
        MarkAsUpdated();
        return Result.Success();
    }
}
