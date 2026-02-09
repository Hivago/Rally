using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.Infrastructure;

/// <summary>
/// Publishes domain events to MediatR after SaveChanges succeeds.
/// This is the "messenger" that connects modules!
/// </summary>
public sealed class DomainEventInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DomainEventInterceptor> _logger;

    public DomainEventInterceptor(
        IPublisher publisher,
        ILogger<DomainEventInterceptor> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await PublishDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return result;
    }

    private async Task PublishDomainEventsAsync(
        Microsoft.EntityFrameworkCore.DbContext context,
        CancellationToken cancellationToken)
    {
        // 1. Find all entities with domain events
        var entitiesWithEvents = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        // 2. Collect all events
        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // 3. Clear events from entities (prevent re-publishing)
        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        // 4. Publish each event via MediatR
        foreach (var domainEvent in domainEvents)
        {
            _logger.LogInformation(
                "Publishing domain event: {EventType}",
                domainEvent.GetType().Name);

            try
            {
                await _publisher.Publish(domainEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error publishing {EventType}",
                    domainEvent.GetType().Name);
                // Don't throw - log and continue with other events
            }
        }
    }
}