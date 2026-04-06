using RallyAPI.SharedKernel.Domain;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Domain.Entities;

public sealed class Restaurant : AggregateRoot
{
    public string Name { get; private set; }
    public PhoneNumber Phone { get; private set; }
    public Email Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string AddressLine { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsAcceptingOrders { get; private set; }
    public int AvgPrepTimeMins { get; private set; }
    public TimeOnly OpeningTime { get; private set; }
    public TimeOnly ClosingTime { get; private set; }
    public decimal CommissionPercentage { get; private set; }
    public bool AutoAcceptOrders { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? LogoFileKey { get; private set; }

    // Owner link (multi-outlet support)
    public Guid? OwnerId { get; private set; }

    // Compliance
    public string? FssaiNumber { get; private set; }

    // Restaurant attributes (cuisine/dietary)
    public List<string> CuisineTypes { get; private set; } = new();
    public bool IsPureVeg { get; private set; }
    public bool IsVeganFriendly { get; private set; }
    public bool HasJainOptions { get; private set; }
    public decimal MinOrderAmount { get; private set; }

    // EF Core
    private Restaurant() { }

    private Restaurant(
        string name,
        PhoneNumber phone,
        Email email,
        string passwordHash,
        string addressLine,
        decimal latitude,
        decimal longitude)
    {
        Name = name;
        Phone = phone;
        Email = email;
        PasswordHash = passwordHash;
        AddressLine = addressLine;
        Latitude = latitude;
        Longitude = longitude;
        IsActive = true;
        IsAcceptingOrders = false; // Start as not accepting
        AutoAcceptOrders = false; // Restaurant must opt in
        AvgPrepTimeMins = 20; // Default
        OpeningTime = new TimeOnly(9, 0);
        ClosingTime = new TimeOnly(22, 0);
        CommissionPercentage = 20.0m; // Default 20%
    }

    public static Result<Restaurant> Create(
        string name,
        PhoneNumber phone,
        Email email,
        string passwordHash,
        string addressLine,
        decimal latitude,
        decimal longitude)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Restaurant>(Error.Validation("Restaurant name is required."));

        if (name.Length > 255)
            return Result.Failure<Restaurant>(Error.Validation("Restaurant name is too long."));

        if (string.IsNullOrWhiteSpace(addressLine))
            return Result.Failure<Restaurant>(Error.Validation("Address is required."));

        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<Restaurant>(Error.Validation("Password is required."));

        // Basic India bounds
        if (latitude < 6 || latitude > 38 || longitude < 68 || longitude > 98)
            return Result.Failure<Restaurant>(Error.Validation("Invalid location coordinates."));

        return new Restaurant(name.Trim(), phone, email, passwordHash, addressLine.Trim(), latitude, longitude);
    }

    public Result UpdateProfile(string? name, string? addressLine, PhoneNumber? phone)
    {
        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure(Error.Validation("Name cannot be empty."));
            Name = name.Trim();
        }

        if (addressLine is not null)
        {
            if (string.IsNullOrWhiteSpace(addressLine))
                return Result.Failure(Error.Validation("Address cannot be empty."));
            AddressLine = addressLine.Trim();
        }

        if (phone is not null)
            Phone = phone;

        MarkAsUpdated();
        return Result.Success();
    }

    public Result UpdateLocation(decimal latitude, decimal longitude)
    {
        if (latitude < 6 || latitude > 38 || longitude < 68 || longitude > 98)
            return Result.Failure(Error.Validation("Invalid location coordinates."));

        Latitude = latitude;
        Longitude = longitude;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result SetBusinessHours(TimeOnly openingTime, TimeOnly closingTime)
    {
        if (openingTime >= closingTime)
            return Result.Failure(Error.Validation("Opening time must be before closing time."));

        OpeningTime = openingTime;
        ClosingTime = closingTime;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result SetPrepTime(int minutes)
    {
        if (minutes < 5 || minutes > 120)
            return Result.Failure(Error.Validation("Prep time must be between 5 and 120 minutes."));

        AvgPrepTimeMins = minutes;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result StartAcceptingOrders()
    {
        if (!IsActive)
            return Result.Failure(Error.Validation("Inactive restaurant cannot accept orders."));

        IsAcceptingOrders = true;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result StopAcceptingOrders()
    {
        IsAcceptingOrders = false;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result SetAutoAcceptOrders(bool enabled)
    {
        AutoAcceptOrders = enabled;
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
            return Result.Failure(Error.Validation("Restaurant is already inactive."));

        IsActive = false;
        IsAcceptingOrders = false;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Failure(Error.Validation("Restaurant is already active."));

        IsActive = true;
        MarkAsUpdated();
        return Result.Success();
    }

    public bool IsOpenNow()
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);
        return IsActive && IsAcceptingOrders && now >= OpeningTime && now <= ClosingTime;
    }

    public void UpdateLogoUrl(string logoUrl, string fileKey)
    {
        LogoUrl = logoUrl;
        LogoFileKey = fileKey;
        UpdatedAt = DateTime.UtcNow;
    }

    public Result SetOwner(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
            return Result.Failure(Error.Validation("Owner ID is required."));

        OwnerId = ownerId;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result SetFssaiNumber(string fssaiNumber)
    {
        if (string.IsNullOrWhiteSpace(fssaiNumber))
            return Result.Failure(Error.Validation("FSSAI number is required."));

        fssaiNumber = fssaiNumber.Trim();

        if (fssaiNumber.Length < 14 || fssaiNumber.Length > 20)
            return Result.Failure(Error.Validation("FSSAI number must be between 14 and 20 characters."));

        FssaiNumber = fssaiNumber;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result SetCuisineTypes(List<string> cuisineTypes)
    {
        CuisineTypes = cuisineTypes?.Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList() ?? new();
        MarkAsUpdated();
        return Result.Success();
    }

    public Result SetDietaryAttributes(bool isPureVeg, bool isVeganFriendly, bool hasJainOptions)
    {
        IsPureVeg = isPureVeg;
        IsVeganFriendly = isVeganFriendly;
        HasJainOptions = hasJainOptions;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result SetMinOrderAmount(decimal amount)
    {
        if (amount < 0)
            return Result.Failure(Error.Validation("Minimum order amount cannot be negative."));

        MinOrderAmount = amount;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result SetCommissionPercentage(decimal percentage)
    {
        if (percentage < 0 || percentage > 100)
            return Result.Failure(Error.Validation("Commission percentage must be between 0 and 100."));

        CommissionPercentage = percentage;
        MarkAsUpdated();
        return Result.Success();
    }
}