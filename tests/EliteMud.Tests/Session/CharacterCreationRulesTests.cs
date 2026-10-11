using EliteMud.Application.Session;
using EliteMud.Application.Session.Authentication;
using EliteMud.Data;
using EliteMud.Data.Entities;
using EliteMud.Data.Repositories;
using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Tests.Server;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EliteMud.Tests.Session;

/// <summary>
/// Character creation and selection use the race and class content: menus with the legacy letters, only the
/// race's allowed classes, ids stored on the character.
/// </summary>
public sealed class CharacterCreationRulesTests : IDisposable
{
    private static readonly RulesContent Rules =
        RulesContentLoader.Load(Path.Combine(RealWorld.RepositoryRoot, "content")).Rules;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly EliteMudDbContext _db;
    private readonly AuthenticationHandler _handler;
    private readonly PromptCatalog _prompts = new(Rules);
    private readonly int _accountId;

    public CharacterCreationRulesTests()
    {
        _connection.Open();
        _db = new EliteMudDbContext(new DbContextOptionsBuilder<EliteMudDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        var account = new Account { Username = "test", PasswordHash = "x" };
        _db.Accounts.Add(account);
        _db.SaveChanges();
        _accountId = account.AccountId;

        _handler = new AuthenticationHandler(
            new AccountRepository(_db),
            new CharacterRepository(_db),
            new PlainPasswords(),
            new IpBanService(),
            _prompts,
            Rules);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void RacePrompt_ListsCreationRacesWithLegacyLetters()
    {
        var prompt = _prompts.GetRacePrompt();

        Assert.Contains("[a] Human", prompt);
        Assert.Contains("[b] Troll", prompt);
        Assert.Contains("[m] Draconian", prompt);
        Assert.DoesNotContain("Vampire", prompt);
        Assert.DoesNotContain("Avatar", prompt);
        Assert.Equal(13, Rules.CreationRaces.Count);
    }

    [Fact]
    public async Task PickingARace_ShowsOnlyItsAllowedClasses()
    {
        var (state, message, _) = await _handler.ProcessInputAsync("d", NewSession(ConnectionState.SelectRace));

        Assert.Equal(ConnectionState.SelectClass, state);
        Assert.Contains("[d] Warrior", message);
        Assert.Contains("[f] Monk", message);
        Assert.Contains("[j] Druid", message);
        Assert.Contains("[o] Mariner", message);
        Assert.DoesNotContain("Magic-User", message);
    }

    [Fact]
    public async Task UnknownRaceLetter_AsksAgain()
    {
        var (state, _, session) = await _handler.ProcessInputAsync("z", NewSession(ConnectionState.SelectRace));

        Assert.Equal(ConnectionState.SelectRace, state);
        Assert.Null(session.PendingRace);
    }

    [Fact]
    public async Task ClassNotAllowedForTheRace_IsRefused()
    {
        var session = NewSession(ConnectionState.SelectClass);
        session.PendingRace = "dwarf";

        var (state, _, _) = await _handler.ProcessInputAsync("a", session); // magic-user

        Assert.Equal(ConnectionState.SelectClass, state);
        Assert.Empty(_db.Characters);
    }

    [Fact]
    public async Task CreatedCharacter_StoresContentIds()
    {
        var session = NewSession(ConnectionState.SelectClass);
        session.PendingRace = "half-elf";

        var (state, _, _) = await _handler.ProcessInputAsync("i", session); // wizard

        Assert.Equal(ConnectionState.ShowMotd, state);
        var character = Assert.Single(_db.Characters);
        Assert.Equal("half-elf", character.Race);
        Assert.Equal("wizard", character.CharacterClass);
    }

    [Fact]
    public async Task CharacterWithUnknownClass_IsRefusedAtSelection()
    {
        var session = NewSession(ConnectionState.CharacterSelection);
        session.Characters = new List<CharacterListItem> { new(1, "Odd", 10, "necromancer", "human") };

        var (state, message, updated) = await _handler.ProcessInputAsync("1", session);

        Assert.Equal(ConnectionState.CharacterSelection, state);
        Assert.Contains("unknown class 'necromancer'", message);
        Assert.Null(updated.SelectedCharacterId);
    }

    [Fact]
    public void CharacterList_ShowsClassNames()
    {
        var menu = _prompts.GetCharacterSelectionMenu(new List<CharacterListItem> { new(1, "Xergon", 30, "magic-user", "human") });

        Assert.Contains("Xergon (Level 30 Magic-User)", menu);
    }

    private SessionData NewSession(ConnectionState state) => new()
    {
        State = state,
        AccountId = _accountId,
        PendingCharacterName = "Tester",
        PendingSex = Sex.Male
    };

    private sealed class PlainPasswords : IPasswordService
    {
        public string HashPassword(string password) => password;

        public bool VerifyPassword(string password, string passwordHash) => password == passwordHash;
    }
}
