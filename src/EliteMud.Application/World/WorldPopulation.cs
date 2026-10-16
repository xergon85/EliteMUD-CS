using EliteMud.Game;

namespace EliteMud.Application.World;

/// <summary>
/// Which characters are in the world and which room each is in: the players in the game and the mobs. The only way
/// a character enters, moves between rooms or leaves, so the room lists and <see cref="PlayerState.RoomId"/> and
/// <see cref="MobInstance.RoomId"/> always agree. Players and mobs are listed apart; legacy keeps one people list per
/// room, in which players go first and mobs next to their twins (bubble_char_to_list), and each list here keeps
/// that order.
/// </summary>
public sealed class WorldPopulation
{
    private static readonly IReadOnlyList<PlayerState> Nobody = Array.Empty<PlayerState>();
    private static readonly IReadOnlyList<MobInstance> NoMobs = Array.Empty<MobInstance>();

    private readonly WorldDefinition _world;
    private readonly List<PlayerState> _players = new();
    private readonly Dictionary<int, List<PlayerState>> _roomPlayers = new();
    private readonly Dictionary<int, List<MobInstance>> _roomMobs = new();

    /// <param name="world">The rooms characters can be in.</param>
    public WorldPopulation(WorldDefinition world)
    {
        _world = world;
    }

    /// <summary>Everyone in the game, in the order they entered.</summary>
    public IReadOnlyList<PlayerState> Players => _players;

    /// <summary>The players in a room, newest first (legacy char_to_room puts a player at the head of the room).</summary>
    public IReadOnlyList<PlayerState> PlayersIn(int roomId) =>
        _roomPlayers.TryGetValue(roomId, out var players) ? players : Nobody;

    public bool IsInGame(PlayerState player) => _players.Contains(player);

    /// <summary>Every mob in the world, room by room.</summary>
    public IEnumerable<MobInstance> Mobs => _roomMobs.Values.SelectMany(mobs => mobs);

    /// <summary>The mobs in a room, in legacy order: newest first, but next to an identical mob if there is one.</summary>
    public IReadOnlyList<MobInstance> MobsIn(int roomId) =>
        _roomMobs.TryGetValue(roomId, out var mobs) ? mobs : NoMobs;

    public bool IsInWorld(MobInstance mob) => mob.RoomId != MobInstance.Nowhere;

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
    /// keeps their <see cref="PlayerState.RoomId"/> so a save after leaving still has it. Who they'd reply to is
    /// forgotten, so departed players don't keep each other alive in a chain (legacy frees the character).
    /// </summary>
    public void Leave(PlayerState player)
    {
        if (!_players.Remove(player))
        {
            return;
        }

        StopFightsOf(player);
        player.LastTellSender = null;
        TakeFromRoom(player);
    }

    /// <summary>A mob enters the world in a room, e.g. loaded by a zone reset (legacy char_to_room).</summary>
    public void Enter(MobInstance mob, int roomId)
    {
        if (IsInWorld(mob))
        {
            throw new InvalidOperationException($"{mob.Name} is already in the world");
        }

        RequireRoom(mob, roomId);
        PutInRoom(mob, roomId);
    }

    /// <summary>A mob goes to another room. Its fight stops, as leaving a room stops it in legacy (char_from_room).</summary>
    public void Move(MobInstance mob, int roomId)
    {
        if (!IsInWorld(mob))
        {
            throw new InvalidOperationException($"{mob.Name} is not in the world");
        }

        RequireRoom(mob, roomId);
        if (mob.Fighting != null)
        {
            CombatCalculator.StopFighting(mob);
        }

        TakeFromRoom(mob);
        PutInRoom(mob, roomId);
    }

    /// <summary>
    /// A mob leaves the world, e.g. dying or a zone reset clearing its room. Its fight stops, and so does everyone's
    /// fighting it (legacy extract_char).
    /// </summary>
    public void Leave(MobInstance mob)
    {
        if (!IsInWorld(mob))
        {
            return;
        }

        TakeFromRoom(mob);
        mob.RoomId = MobInstance.Nowhere;
        StopFightsOf(mob);
    }

    /// <summary>
    /// A starting mob, put at the end of its room so the world's starting lists keep the order they were given in.
    /// Everything that happens in play goes through <see cref="Enter(MobInstance, int)"/>.
    /// </summary>
    internal void Place(MobInstance mob, int roomId)
    {
        RequireRoom(mob, roomId);
        RoomMobs(roomId).Add(mob);
        mob.RoomId = roomId;
    }

    private void StopFightsOf(ICombatant leaver)
    {
        CombatCalculator.StopEveryoneFighting(leaver, _players.Concat<ICombatant>(Mobs));
        if (leaver.Fighting != null)
        {
            CombatCalculator.StopFighting(leaver);
        }
    }

    // Checked before anything changes, so a refused enter or move leaves everything as it was
    private void RequireRoom(ICombatant character, int roomId)
    {
        if (!_world.Rooms.ContainsKey(roomId))
        {
            throw new ArgumentException($"No room {roomId} for {character.Name}", nameof(roomId));
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

    // Legacy bubble_char_to_list: in front of the first identical mob, otherwise at the head
    private void PutInRoom(MobInstance mob, int roomId)
    {
        var mobs = RoomMobs(roomId);
        var twin = mobs.FindIndex(other => IsTwin(mob, other));
        mobs.Insert(twin < 0 ? 0 : twin, mob);
        mob.RoomId = roomId;
    }

    private void TakeFromRoom(MobInstance mob)
    {
        if (_roomMobs.TryGetValue(mob.RoomId, out var mobs))
        {
            mobs.Remove(mob);
        }
    }

    private List<MobInstance> RoomMobs(int roomId)
    {
        if (!_roomMobs.TryGetValue(roomId, out var mobs))
        {
            mobs = new List<MobInstance>();
            _roomMobs[roomId] = mobs;
        }

        return mobs;
    }

    // Legacy compares the vnum, affected_by and short description. The short description and the definition's own
    // flags come with the vnum, so what's left is the flags the mob's affects set now.
    private static bool IsTwin(MobInstance mob, MobInstance other) =>
        mob.Definition.Id == other.Definition.Id && AffectedFlags(mob).SetEquals(AffectedFlags(other));

    private static HashSet<AffectType> AffectedFlags(MobInstance mob) =>
        mob.Affects.Select(affect => affect.Type).Where(type => type.SetsAffectedFlag()).ToHashSet();
}
