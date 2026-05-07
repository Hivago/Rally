using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Users.Domain.Events;

public sealed class RiderWentOfflineEvent : BaseDomainEvent
{
    public Guid RiderId { get; }
    public string Name { get; }

    public RiderWentOfflineEvent(Guid riderId, string name)
    {
        RiderId = riderId;
        Name = name;
    }
}
