using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Users.Domain.Events;

public sealed class RiderWentOnlineEvent : BaseDomainEvent
{
    public Guid RiderId { get; }
    public string Name { get; }

    public RiderWentOnlineEvent(Guid riderId, string name)
    {
        RiderId = riderId;
        Name = name;
    }
}
