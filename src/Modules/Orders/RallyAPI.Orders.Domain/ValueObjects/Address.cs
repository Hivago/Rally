using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Orders.Domain.ValueObjects;

/// <summary>
/// Represents a delivery address with coordinates.
/// Immutable value object - create new instance for changes.
/// </summary>
public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string Pincode { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public string? Landmark { get; }
    public string? BuildingName { get; }
    public string? Floor { get; }
    public string? ContactPhone { get; }

    public string? FullAddress => GetFormattedAddress();

    // Additional fields for flexibility
    public string? Instructions { get; }

    private Address(
        string street,
        string city,
        string pincode,
        double latitude,
        double longitude,
        string? landmark,
        string? buildingName,
        string? floor,
        string? contactPhone,
        string? instructions)
    {
        Street = street;
        City = city;
        Pincode = pincode;
        Latitude = latitude;
        Longitude = longitude;
        Landmark = landmark;
        BuildingName = buildingName;
        Floor = floor;
        ContactPhone = contactPhone;
        Instructions = instructions;
    }

    public static Address Create(
        string street,
        string city,
        string pincode,
        double latitude,
        double longitude,
        string? landmark = null,
        string? buildingName = null,
        string? floor = null,
        string? contactPhone = null,
        string? instructions = null)
    {
        // Basic validation - keep flexible for different regions
        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street is required", nameof(street));

        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required", nameof(city));

        if (string.IsNullOrWhiteSpace(pincode))
            throw new ArgumentException("Pincode is required", nameof(pincode));

        if (latitude < -90 || latitude > 90)
            throw new ArgumentException("Invalid latitude", nameof(latitude));

        if (longitude < -180 || longitude > 180)
            throw new ArgumentException("Invalid longitude", nameof(longitude));

        return new Address(
            street.Trim(),
            city.Trim(),
            pincode.Trim(),
            latitude,
            longitude,
            landmark?.Trim(),
            buildingName?.Trim(),
            floor?.Trim(),
            contactPhone?.Trim(),
            instructions?.Trim());
    }

    /// <summary>
    /// Returns formatted address for display
    /// </summary>
    public string GetFormattedAddress()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(BuildingName))
            parts.Add(BuildingName);

        if (!string.IsNullOrWhiteSpace(Floor))
            parts.Add($"Floor {Floor}");

        parts.Add(Street);

        if (!string.IsNullOrWhiteSpace(Landmark))
            parts.Add($"Near {Landmark}");

        parts.Add(City);
        parts.Add(Pincode);

        return string.Join(", ", parts);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street.ToLowerInvariant();
        yield return City.ToLowerInvariant();
        yield return Pincode;
        yield return Latitude;
        yield return Longitude;
    }

    public override string ToString() => GetFormattedAddress();
}