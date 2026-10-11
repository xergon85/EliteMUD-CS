using EliteMud.Application.World;
using EliteMud.Data;
using EliteMud.Data.Entities;
using EliteMud.Data.Repositories;
using EliteMud.Game;
using EliteMud.Server;
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
        _queue = new CharacterSaveQueue(_services, world);
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

    private int StoredLevel()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EliteMudDbContext>();
        return db.Characters.AsNoTracking().Single(c => c.CharacterId == _characterId).Level;
    }
}
