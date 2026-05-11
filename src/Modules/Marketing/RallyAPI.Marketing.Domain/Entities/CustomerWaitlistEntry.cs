using RallyAPI.SharedKernel.Domain;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Domain.Entities;

public sealed class CustomerWaitlistEntry : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Source { get; private set; }
    public string? IpAddress { get; private set; }

    private CustomerWaitlistEntry() { }

    public static Result<CustomerWaitlistEntry> Create(
        string name,
        string email,
        string phone,
        string? source = null,
        string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<CustomerWaitlistEntry>(Error.Validation("Name is required."));

        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<CustomerWaitlistEntry>(Error.Validation("Email is required."));

        if (string.IsNullOrWhiteSpace(phone))
            return Result.Failure<CustomerWaitlistEntry>(Error.Validation("Phone is required."));

        return Result.Success(new CustomerWaitlistEntry
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Phone = phone.Trim(),
            Source = source?.Trim(),
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }
}
