using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Data;
using EliteMud.Data.Entities;
using EliteMud.Data.Repositories;
using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Save;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Tests.Server;

/// <summary>
/// The save queue may skip a request only when a later save already stored the newer state.
/// </summary>
public sealed class CharacterSaveQueueTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services;
    private readonly CharacterSaveQueue _queue;
    private readonly GameStateGate _gate = new();
    private readonly int _characterId;

    public CharacterSaveQueueTests()
    {
        _connection.Open();
        _services = new ServiceCollection()
            .AddDbContext<EliteMudDbContext>(options => options.UseSqlite(_connection))
            .AddScoped<ICharacterRepository, CharacterRepository>()
            .BuildServiceProvider();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EliteMudDbContext>();
            db.Database.EnsureCreated();
            var account = new Account { Username = "test", PasswordHash = "x" };
            db.Accounts.Add(account);
            db.SaveChanges();
            var character = new Character
            {
                AccountId = account.AccountId, Name = "Tester", Race = "human", CharacterClass = "warrior", Level = 1
            };
            db.Characters.Add(character);
            db.SaveChanges();
            _characterId = character.CharacterId;
        }

        var world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>()),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>>(),
            new Dictionary<int, List<ObjectInstance>>(),
            new List<ZoneDefinition>());
        _queue = new CharacterSaveQueue(_services, _gate);
    }

    public void Dispose()
    {
        _queue.Dispose();
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ChangeRightAfterASave_IsStillSaved()
    {
        var player = new PlayerState(_characterId, "Tester", 3001, level: 1, "warrior", "human", sex: 1);
        Assert.True(await _queue.QueueSaveAndWaitAsync(_characterId, player));

        // e.g. a level-up just after an autosave: within the old 2-second window, which used to drop it
        player.Level = 2;
        Assert.True(await _queue.QueueSaveAndWaitAsync(_characterId, player));

        Assert.Equal(2, StoredLevel());
    }

    /// <summary>A save reads the player under the game state gate, so the game can't change them mid-copy (#61).</summary>
    [Fact]
    public async Task ASave_WaitsWhileTheGameHoldsTheGate()
    {
        var player = new PlayerState(_characterId, "Tester", 3001, level: 1, "warrior", "human", sex: 1);
        Task<bool> saved;

        using (await _gate.EnterAsync(CancellationToken.None))
        {
            saved = _queue.QueueSaveAndWaitAsync(_characterId, player);
            await Task.Delay(200);
            Assert.False(saved.IsCompleted);
            player.Level = 3; // still the game's turn: this change is part of the save
        }

        Assert.True(await saved.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(3, StoredLevel());
    }

    /// <summary>
    /// The save command runs under the gate (like every command) and the save needs the gate, so the command
    /// can't wait for it: it reports when the save is done instead of deadlocking (#61).
    /// </summary>
    [Fact]
    public async Task TheSaveCommand_ReportsWhenDone_WithoutHoldingUpTheGame()
    {
        var player = new PlayerState(_characterId, "Tester", 3001, level: 1, "warrior", "human", sex: 1);
        using var connection = new TestConnection(1, player);
        var context = new ConnectionContext(1, connection.Context.Session, player, _characterId, "test");
        var save = new SaveCommandHandler(_queue);

        using (await _gate.EnterAsync(CancellationToken.None))
        {
            var outcome = save.HandleAsync(new CommandRequest("save", null, null), context, CancellationToken.None);
            Assert.True(outcome.IsCompleted);
        }

        var lines = new List<string>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!lines.Contains("Character saved.") && DateTime.UtcNow < deadline)
        {
            lines.AddRange(connection.Lines());
        }

        Assert.Contains("Character saved.", lines);
    }

    [Fact]
    public async Task ACancelledSave_SaysSo()
    {
        var player = new PlayerState(_characterId, "Tester", 3001, level: 1, "warrior", "human", sex: 1);
        using var connection = new TestConnection(1, player);
        var context = new ConnectionContext(1, connection.Context.Session, player, _characterId, "test");
        using var shutdown = new CancellationTokenSource();
        shutdown.Cancel();

        await new SaveCommandHandler(_queue).HandleAsync(new CommandRequest("save", null, null), context, shutdown.Token);

        var lines = new List<string>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (lines.Count == 0 && DateTime.UtcNow < deadline)
        {
            lines.AddRange(connection.Lines());
        }

        Assert.Equal(new[] { "Your character wasn't saved: the server is shutting down." }, lines);
    }

    private int StoredLevel()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EliteMudDbContext>();
        return db.Characters.AsNoTracking().Single(c => c.CharacterId == _characterId).Level;
    }
}
