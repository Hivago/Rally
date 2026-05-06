using RallyAPI.SharedKernel.Domain;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Domain.Events;

public sealed class RiderKycSubmittedEvent : BaseDomainEvent
{
    public Guid RiderId { get; }
    public string Name { get; }
    public RiderKycDocumentType DocumentType { get; }

    public RiderKycSubmittedEvent(Guid riderId, string name, RiderKycDocumentType documentType)
    {
        RiderId = riderId;
        Name = name;
        DocumentType = documentType;
    }
}
