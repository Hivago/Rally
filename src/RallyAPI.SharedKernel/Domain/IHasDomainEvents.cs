namespace RallyAPI.SharedKernel.Domain;

/// <summary>
/// Contract for entities that can raise domain events.
/// The Interceptor looks for this to know which entities have events.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>
    /// Gets all pending domain events
    /// </summary>
    // In IHasDomainEvents.cs - change to:
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    /// <summary>
    /// Clears events after publishing (prevents duplicate publishing)
    /// </summary>
    void ClearDomainEvents();
}