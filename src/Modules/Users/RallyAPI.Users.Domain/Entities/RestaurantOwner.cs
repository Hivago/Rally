using RallyAPI.SharedKernel.Domain;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Domain.Entities;

public sealed class RestaurantOwner : AggregateRoot
{
    public string Name { get; private set; }
    public Email Email { get; private set; }
    public string PasswordHash { get; private set; }
    public PhoneNumber Phone { get; private set; }
    public string? PanNumber { get; private set; }
    public string? GstNumber { get; private set; }
    public string? BankAccountNumber { get; private set; }
    public string? BankIfscCode { get; private set; }
    public string? BankAccountName { get; private set; }
    public bool IsActive { get; private set; }

    // EF Core
    private RestaurantOwner() { }

    private RestaurantOwner(
        string name,
        Email email,
        string passwordHash,
        PhoneNumber phone)
    {
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Phone = phone;
        IsActive = true;
    }

    public static Result<RestaurantOwner> Create(
        string name,
        Email email,
        string passwordHash,
        PhoneNumber phone)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<RestaurantOwner>(Error.Validation("Owner name is required."));

        if (name.Length > 255)
            return Result.Failure<RestaurantOwner>(Error.Validation("Owner name is too long."));

        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<RestaurantOwner>(Error.Validation("Password is required."));

        return new RestaurantOwner(name.Trim(), email, passwordHash, phone);
    }

    public Result UpdateProfile(string? name, PhoneNumber? phone)
    {
        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure(Error.Validation("Name cannot be empty."));
            Name = name.Trim();
        }

        if (phone is not null)
            Phone = phone;

        MarkAsUpdated();
        return Result.Success();
    }

    public Result SetPanNumber(string panNumber)
    {
        if (string.IsNullOrWhiteSpace(panNumber))
            return Result.Failure(Error.Validation("PAN number is required."));

        panNumber = panNumber.Trim().ToUpperInvariant();

        if (panNumber.Length != 10)
            return Result.Failure(Error.Validation("PAN number must be exactly 10 characters."));

        PanNumber = panNumber;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result SetGstNumber(string gstNumber)
    {
        if (string.IsNullOrWhiteSpace(gstNumber))
            return Result.Failure(Error.Validation("GST number is required."));

        gstNumber = gstNumber.Trim().ToUpperInvariant();

        if (gstNumber.Length != 15)
            return Result.Failure(Error.Validation("GST number must be exactly 15 characters."));

        GstNumber = gstNumber;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result UpdateBankDetails(string accountNumber, string ifscCode, string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Result.Failure(Error.Validation("Bank account number is required."));

        if (string.IsNullOrWhiteSpace(ifscCode))
            return Result.Failure(Error.Validation("IFSC code is required."));

        if (string.IsNullOrWhiteSpace(accountName))
            return Result.Failure(Error.Validation("Account holder name is required."));

        ifscCode = ifscCode.Trim().ToUpperInvariant();

        if (ifscCode.Length != 11)
            return Result.Failure(Error.Validation("IFSC code must be exactly 11 characters."));

        BankAccountNumber = accountNumber.Trim();
        BankIfscCode = ifscCode;
        BankAccountName = accountName.Trim();
        MarkAsUpdated();
        return Result.Success();
    }

    public Result UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            return Result.Failure(Error.Validation("Password cannot be empty."));

        PasswordHash = newPasswordHash;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Failure(Error.Validation("Owner is already inactive."));

        IsActive = false;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Failure(Error.Validation("Owner is already active."));

        IsActive = true;
        MarkAsUpdated();
        return Result.Success();
    }
}
