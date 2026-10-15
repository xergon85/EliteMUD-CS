using System.Collections.Concurrent;
using EliteMud.Game;

namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>The connections of the players in the game, by player.</summary>
internal sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<PlayerState, ConnectionContext> _connections =
        new(ReferenceEqualityComparer.Instance);

    public IEnumerable<ConnectionContext> GetConnections() => _connections.Values;

    public int Count => _connections.Count;

    public void Add(ConnectionContext connection) => _connections[connection.Player] = connection;

    public void Remove(ConnectionContext connection) => _connections.TryRemove(connection.Player, out _);

    /// <summary>The character's connection, or null for a mob or a player without one.</summary>
    public ConnectionContext? Find(ICombatant character) =>
        character is PlayerState player && _connections.TryGetValue(player, out var connection) ? connection : null;
}
