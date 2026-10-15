using EliteMud.Application.Commands.Who;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Game;

/// <summary>
/// The world tracks which players are in the game and where (#79), with legacy char_to_room, char_from_room and
/// extract_char's rules for fights.
/// </summary>
public class WorldPopulationTests
{
    private const int Temple = 1;
    private const int Square = 2;

    private readonly WorldState _world;
    private readonly MobInstance _mob;

    public WorldPopulationTests()
    {
        var definition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null);
        _mob = new MobInstance(1, definition);
        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Temple] = new(Temple, "Temple", "Desc", new List<ExitDefinition>()),
                [Square] = new(Square, "Square", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition> { [100] = definition },
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Temple] = new() { _mob }, [Square] = new() },
            new Dictionary<int, List<ObjectInstance>> { [Temple] = new(), [Square] = new() },
            new List<ZoneDefinition>());
    }

    private WorldPopulation Population => _world.Population;

    [Fact]
    public void Entering_PutsThePlayerAtTheHeadOfTheRoom()
    {
        var first = Player(1, "First");
        var second = Player(2, "Second");

        Population.Enter(first, Temple);
        Population.Enter(second, Temple);

        Assert.Equal(new[] { second, first }, Population.PlayersIn(Temple));
        Assert.Equal(new[] { first, second }, Population.Players);
        Assert.Empty(Population.PlayersIn(Square));
    }

    [Fact]
    public void Entering_TwiceOrIntoNoRoom_Fails()
    {
        var player = Player(1, "Tester");
        Population.Enter(player, Temple);

        Assert.Throws<InvalidOperationException>(() => Population.Enter(player, Temple));

        var lost = Player(2, "Lost");
        Assert.Throws<ArgumentException>(() => Population.Enter(lost, 999));
        Assert.False(Population.IsInGame(lost));
        Population.Enter(lost, Temple);
    }

    /// <summary>A move to no room is refused before anything changes: the player stays put, still fighting.</summary>
    [Fact]
    public void Moving_IntoNoRoom_LeavesThePlayerWhereTheyWere()
    {
        var player = Player(1, "Tester");
        Population.Enter(player, Temple);
        player.Fighting = _mob;

        Assert.Throws<ArgumentException>(() => Population.Move(player, 999));

        Assert.Equal(Temple, player.RoomId);
        Assert.Equal(new[] { player }, Population.PlayersIn(Temple));
        Assert.Same(_mob, player.Fighting);
    }

    [Fact]
    public void Moving_TakesThePlayerToTheHeadOfTheNewRoom()
    {
        var mover = Player(1, "Mover");
        var waiting = Player(2, "Waiting");
        Population.Enter(mover, Temple);
        Population.Enter(waiting, Square);

        Population.Move(mover, Square);

        Assert.Equal(Square, mover.RoomId);
        Assert.Empty(Population.PlayersIn(Temple));
        Assert.Equal(new[] { mover, waiting }, Population.PlayersIn(Square));
    }

    [Fact]
    public void Moving_SomeoneNotInTheGame_Fails()
    {
        Assert.Throws<InvalidOperationException>(() => Population.Move(Player(1, "Ghost"), Square));
    }

    /// <summary>Legacy char_from_room stops the mover's fight; whoever they fought keeps at it.</summary>
    [Fact]
    public void Moving_StopsTheMoversFight_ButNotTheirOpponents()
    {
        var mover = Player(1, "Mover");
        Population.Enter(mover, Temple);
        mover.Fighting = _mob;
        mover.Position = Position.Fighting;
        _mob.Fighting = mover;

        Population.Move(mover, Square);

        Assert.Null(mover.Fighting);
        Assert.Equal(Position.Standing, mover.Position);
        Assert.Same(mover, _mob.Fighting);
    }

    /// <summary>Only stop_fighting stands someone up: a resting player who isn't fighting stays resting.</summary>
    [Fact]
    public void Moving_WithoutAFight_KeepsThePosition()
    {
        var mover = Player(1, "Mover");
        Population.Enter(mover, Temple);
        mover.Position = Position.Resting;

        Population.Move(mover, Square);

        Assert.Equal(Position.Resting, mover.Position);
    }

    /// <summary>
    /// Legacy extract_char: the leaver's fight stops, and so does everyone's fighting them, players and mobs alike.
    /// Other fights go on.
    /// </summary>
    [Fact]
    public void Leaving_StopsTheLeaversFight_AndEveryoneFightingThem()
    {
        var leaver = Player(1, "Leaver");
        var attacker = Player(2, "Attacker");
        var bystander = Player(3, "Bystander");
        var other = Player(4, "Other");
        foreach (var player in new[] { leaver, attacker, bystander, other })
        {
            Population.Enter(player, Temple);
        }

        leaver.Fighting = _mob;
        _mob.Fighting = leaver;
        attacker.Fighting = leaver;
        bystander.Fighting = other;

        Population.Leave(leaver);

        Assert.Null(leaver.Fighting);
        Assert.Null(_mob.Fighting);
        Assert.Null(attacker.Fighting);
        Assert.Same(other, bystander.Fighting);
    }

    /// <summary>
    /// Mobs remember a player who quits: legacy forgets only when the mob kills them. The player keeps their room for
    /// the save that follows.
    /// </summary>
    [Fact]
    public void Leaving_TakesThePlayerOutOfTheGame_ButMobsStillRememberThem()
    {
        var leaver = Player(1, "Leaver");
        Population.Enter(leaver, Square);
        _mob.RememberPlayer(leaver.Id);

        Population.Leave(leaver);

        Assert.False(Population.IsInGame(leaver));
        Assert.Empty(Population.Players);
        Assert.Empty(Population.PlayersIn(Square));
        Assert.Equal(Square, leaver.RoomId);
        Assert.Contains(leaver.Id, _mob.Memory);
    }

    [Fact]
    public void Who_ListsThePlayersInTheGame()
    {
        Population.Enter(Player(1, "Ingame"), Temple);
        var gone = Player(2, "Gone");
        Population.Enter(gone, Temple);
        Population.Leave(gone);

        var who = new WhoHandler(_world, RulesContent.Empty).Handle().Message;

        Assert.Contains("Ingame", who);
        Assert.DoesNotContain("Gone", who);
    }

    private static PlayerState Player(int id, string name) =>
        new(id, name, Temple, level: 5, "warrior", "human", sex: 1);
}
