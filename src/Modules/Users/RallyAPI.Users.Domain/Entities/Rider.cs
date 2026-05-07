using RallyAPI.SharedKernel.Domain;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.Events;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Domain.Entities;

public sealed class Rider : AggregateRoot
{
    public PhoneNumber Phone { get; private set; }
    public string Name { get; private set; }
    public Email? Email { get; private set; }
    public VehicleType VehicleType { get; private set; }
    public string? VehicleNumber { get; private set; }
    public KycStatus KycStatus { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsOnline { get; private set; }
    public decimal? CurrentLatitude { get; private set; }
    public decimal? CurrentLongitude { get; private set; }
    public DateTime? LastLocationUpdate { get; private set; }
    /// <summary>
    /// Current delivery assignment. Null if rider is available.
    /// </summary>
    public Guid? CurrentDeliveryId { get; private set; }

    /// <summary>
    /// When the current delivery was assigned.
    /// </summary>
    public DateTime? CurrentDeliveryAssignedAt { get; private set; }

    private readonly List<RiderKycDocument> _kycDocuments = [];
    public IReadOnlyList<RiderKycDocument> KycDocuments => _kycDocuments.AsReadOnly();


    // EF Core
    private Rider() { }

    private Rider(PhoneNumber phone, string name, VehicleType vehicleType)
    {
        Phone = phone;
        Name = name;
        VehicleType = vehicleType;
        KycStatus = KycStatus.Pending;
        IsActive = true;
        IsOnline = false;

        AddDomainEvent(new RiderCreatedEvent(Id, phone.Value));
    }

    public static Result<Rider> Create(PhoneNumber phone, string name, VehicleType vehicleType)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Rider>(Error.Validation("Rider name is required."));

        if (name.Length > 100)
            return Result.Failure<Rider>(Error.Validation("Rider name is too long."));

        return new Rider(phone, name.Trim(), vehicleType);
    }

    public Result UpdateProfile(string? name, Email? email, string? vehicleNumber)
    {
        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure(Error.Validation("Name cannot be empty."));
            if (name.Length > 100)
                return Result.Failure(Error.Validation("Name is too long."));
            Name = name.Trim();
        }

        if (vehicleNumber is not null)
        {
            if (vehicleNumber.Length > 20)
                return Result.Failure(Error.Validation("Vehicle number is too long."));
            VehicleNumber = vehicleNumber.Trim().ToUpperInvariant();
        }

        Email = email;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result GoOnline()
    {
        if (!IsActive)
            return Result.Failure(Error.Validation("Inactive rider cannot go online."));

        if (KycStatus != KycStatus.Verified)
            return Result.Failure(Error.Validation("KYC must be verified to go online."));

        var wasOnline = IsOnline;
        IsOnline = true;
        MarkAsUpdated();

        if (!wasOnline)
            AddDomainEvent(new RiderWentOnlineEvent(Id, Name));

        return Result.Success();
    }

    public Result GoOffline()
    {
        var wasOnline = IsOnline;
        IsOnline = false;
        MarkAsUpdated();

        if (wasOnline)
            AddDomainEvent(new RiderWentOfflineEvent(Id, Name));

        return Result.Success();
    }

    public Result UpdateLocation(decimal latitude, decimal longitude)
    {
        if (!IsOnline)
            return Result.Failure(Error.Validation("Rider must be online to update location."));

        // Basic India bounds validation
        if (latitude < 6 || latitude > 38)
            return Result.Failure(Error.Validation("Invalid latitude."));

        if (longitude < 68 || longitude > 98)
            return Result.Failure(Error.Validation("Invalid longitude."));

        CurrentLatitude = latitude;
        CurrentLongitude = longitude;
        LastLocationUpdate = DateTime.UtcNow;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result UpdateKycStatus(KycStatus newStatus)
    {
        // Cannot go back to pending once verified/rejected
        if (KycStatus == KycStatus.Verified && newStatus == KycStatus.Pending)
            return Result.Failure(Error.Validation("Cannot revert verified KYC to pending."));

        KycStatus = newStatus;

        // If KYC rejected, force offline
        if (newStatus == KycStatus.Rejected)
            IsOnline = false;

        MarkAsUpdated();
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Failure(Error.Validation("Rider is already inactive."));

        IsActive = false;
        IsOnline = false; // Force offline
        MarkAsUpdated();
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Failure(Error.Validation("Rider is already active."));

        IsActive = true;
        MarkAsUpdated();
        return Result.Success();
    }

    // ─── ADD THESE METHODS (near GoOnline/GoOffline methods) ───

    /// <summary>
    /// Assigns a delivery to this rider.
    /// Rider must be online, active, KYC verified, and not already on a delivery.
    /// </summary>
    /// <param name="deliveryRequestId">The delivery request to assign</param>
    /// <returns>Success or failure with error</returns>
    public Result AssignDelivery(Guid deliveryRequestId)
    {
        // Validate rider can accept deliveries
        if (!IsActive)
            return Result.Failure(Error.Validation(
                "Rider.NotActive",
                "Inactive rider cannot be assigned deliveries."));

        if (KycStatus != KycStatus.Verified)
            return Result.Failure(Error.Validation(
                "Rider.KycNotVerified",
                "Rider KYC must be verified to accept deliveries."));

        if (!IsOnline)
            return Result.Failure(Error.Validation(
                "Rider.NotOnline",
                "Rider must be online to accept deliveries."));

        if (CurrentDeliveryId.HasValue)
            return Result.Failure(Error.Validation(
                "Rider.AlreadyOnDelivery",
                $"Rider is already on delivery {CurrentDeliveryId.Value}."));

        // Assign the delivery
        CurrentDeliveryId = deliveryRequestId;
        CurrentDeliveryAssignedAt = DateTime.UtcNow;

        MarkAsUpdated();

        // Optionally raise domain event
        // AddDomainEvent(new RiderDeliveryAssignedEvent(Id, deliveryRequestId));

        return Result.Success();
    }

    /// <summary>
    /// Clears the current delivery assignment.
    /// Called when delivery is completed, failed, or reassigned.
    /// </summary>
    /// <param name="deliveryRequestId">Expected delivery ID (for validation)</param>
    /// <returns>Success or failure with error</returns>
    public Result ClearDelivery(Guid deliveryRequestId)
    {
        if (!CurrentDeliveryId.HasValue)
            return Result.Failure(Error.Validation(
                "Rider.NoActiveDelivery",
                "Rider has no active delivery to clear."));

        if (CurrentDeliveryId.Value != deliveryRequestId)
            return Result.Failure(Error.Validation(
                "Rider.DeliveryMismatch",
                $"Rider's current delivery {CurrentDeliveryId.Value} doesn't match {deliveryRequestId}."));

        CurrentDeliveryId = null;
        CurrentDeliveryAssignedAt = null;

        MarkAsUpdated();

        // Optionally raise domain event
        // AddDomainEvent(new RiderDeliveryClearedEvent(Id, deliveryRequestId));

        return Result.Success();
    }

    /// <summary>
    /// Force clears delivery without validation.
    /// Use only for admin/system operations.
    /// </summary>
    public Result ForceClearDelivery()
    {
        CurrentDeliveryId = null;
        CurrentDeliveryAssignedAt = null;

        MarkAsUpdated();

        return Result.Success();
    }

    /// <summary>
    /// Checks if rider is available for new deliveries.
    /// </summary>
    public bool IsAvailableForDelivery()
    {
        return IsActive
            && IsOnline
            && KycStatus == KycStatus.Verified
            && !CurrentDeliveryId.HasValue;
    }

    public RiderKycDocument AddOrReplaceKycDocument(
    RiderKycDocumentType documentType,
    string fileKey,
    string publicUrl)
    {
        // Remove existing document of same type if present
        var existing = _kycDocuments.FirstOrDefault(d => d.DocumentType == documentType);
        if (existing is not null)
            _kycDocuments.Remove(existing);

        var document = RiderKycDocument.Create(Id, documentType, fileKey, publicUrl);
        _kycDocuments.Add(document);
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new RiderKycSubmittedEvent(Id, Name, documentType));

        return document;
    }
}