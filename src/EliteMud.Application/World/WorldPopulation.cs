using EliteMud.Game;

namespace EliteMud.Application.World;

/// <summary>
/// Which players are in the game and which room each is in. The only way a player enters the game, moves between
/// rooms or leaves it, so the room lists and <see cref="PlayerState.RoomId"/> always agree. Mobs still live in
/// <see cref="WorldState"/>'s room lists (#79).
/// </summary>
public sealed class WorldPopulation
{
    private static readonly IReadOnlyList<PlayerState> Nobody = Array.Empty<PlayerState>();

    private readonly WorldDefinition _world;
    private readonly Func<IEnumerable<MobInstance>> _mobs;
    private readonly List<PlayerState> _players = new();
    private readonly Dictionary<int, List<PlayerState>> _roomPlayers = new();

    /// <param name="world">The rooms characters can be in.</param>
    /// <param name="mobs">Every mob in the world, to stop those fighting a player who leaves.</param>
    public WorldPopulation(WorldDefinition world, Func<IEnumerable<MobInstance>> mobs)
    {
        _world = world;
        _mobs = mobs;
    }

    /// <summary>Everyone in the game, in the order they entered.</summary>
    public IReadOnlyList<PlayerState> Players => _players;

    /// <summary>The players in a room, newest first (legacy char_to_room puts a player at the head of the room).</summary>
    public IReadOnlyList<PlayerState> PlayersIn(int roomId) =>
        _roomPlayers.TryGetValue(roomId, out var players) ? players : Nobody;

    public bool IsInGame(PlayerState player) => _players.Contains(player);

    /// <summary>A player enters the game in a room, e.g. on login.</summary>
    public void Enter(PlayerState player, int roomId)
    {
        if (IsInGame(player))
        {
            throw new InvalidOperationException($"{player.Name} is already in the game");
        }

        RequireRoom(player, roomId);
        _players.Add(player);
        PutInRoom(player, roomId);
    }

    /// <summary>
    /// A player goes to another room. Their fight stops, as leaving a room stops it in legacy (char_from_room).
    /// Those fighting them stop only if the caller says so, e.g. fleeing.
    /// </summary>
    public void Move(PlayerState player, int roomId)
    {
        if (!IsInGame(player))
        {
            throw new InvalidOperationException($"{player.Name} is not in the game");
        }

        RequireRoom(player, roomId);
        if (player.Fighting != null)
        {
            CombatCalculator.StopFighting(player);
        }

        TakeFromRoom(player);
        PutInRoom(player, roomId);
    }

    /// <summary>
    /// A player leaves the game, e.g. quitting or losing their link. Their fight stops, and so does everyone's
    /// fighting them (legacy extract_char). Mobs still remember them: legacy forgets only on a kill. The player
    /// keeps their <see cref="PlayerState.RoomId"/> so a save after leaving still has it.
    /// </summary>
    public void Leave(PlayerState player)
    {
        if (!_players.Remove(player))
        {
            return;
        }

        CombatCalculator.StopEveryoneFighting(player, _players.Concat<ICombatant>(_mobs()));
        if (player.Fighting != null)
        {
            CombatCalculator.StopFighting(player);
        }

        TakeFromRoom(player);
    }

    // Checked before anything changes, so a refused enter or move leaves everything as it was
    private void RequireRoom(PlayerState player, int roomId)
    {
        if (!_world.Rooms.ContainsKey(roomId))
        {
            throw new ArgumentException($"No room {roomId} for {player.Name}", nameof(roomId));
        }
    }

    private void PutInRoom(PlayerState player, int roomId)
    {
        if (!_roomPlayers.TryGetValue(roomId, out var players))
        {
            players = new List<PlayerState>();
            _roomPlayers[roomId] = players;
        }

        players.Insert(0, player);
        player.RoomId = roomId;
    }

    private void TakeFromRoom(PlayerState player)
    {
        if (_roomPlayers.TryGetValue(player.RoomId, out var players))
        {
            players.Remove(player);
        }
    }
}
