using System.Collections.Concurrent;

namespace RallyAPI.Host.Hubs;

/// <summary>
/// Singleton that tracks active SignalR connection IDs per user.
/// Backs IsRiderConnectedAsync and group cleanup on disconnect.
/// </summary>
public sealed class ConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();
    private readonly object _lock = new();

    public void AddConnection(Guid userId, string connectionId)
    {
        _connections.AddOrUpdate(
            userId,
            _ => [connectionId],
            (_, set) =>
            {
                lock (_lock) { set.Add(connectionId); }
                return set;
            });
    }

    public void RemoveConnection(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set))
            return;

        lock (_lock)
        {
            set.Remove(connectionId);
            if (set.Count == 0)
                _connections.TryRemove(userId, out _);
        }
    }

    public bool IsConnected(Guid userId) =>
        _connections.TryGetValue(userId, out var set) && set.Count > 0;
}
