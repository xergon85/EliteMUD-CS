using EliteMud.Data;
using EliteMud.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EliteMud.Tests.Session;

/// <summary>
/// The data migration from enum names ("HalfElf", "MagicUser") to content ids ("half-elf", "magic-user").
/// </summary>
public sealed class StoreClassAndRaceAsContentIdsTests : IDisposable
{
    private const string PreviousMigration = "20260125120339_AddEquipmentItemData";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly EliteMudDbContext _db;

    public StoreClassAndRaceAsContentIdsTests()
    {
        _connection.Open();
        _db = new EliteMudDbContext(new DbContextOptionsBuilder<EliteMudDbContext>().UseSqlite(_connection).Options);
        Migrator.Migrate(PreviousMigration);

        var account = new Account { Username = "test", PasswordHash = "x" };
        _db.Accounts.Add(account);
        _db.SaveChanges();
        AddCharacter(account, "Mage", "HalfElf", "MagicUser");
        AddCharacter(account, "Ninja", "Lizardman", "Ninja");
        AddCharacter(account, "Odd", "Human", "Necromancer"); // not an enum name: left alone
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private IMigrator Migrator => _db.GetService<IMigrator>();

    [Fact]
    public void Up_RenamesEnumNamesToContentIds()
    {
        Migrator.Migrate();

        Assert.Equal(("half-elf", "magic-user"), Stored("Mage"));
        Assert.Equal(("lizard", "ninja"), Stored("Ninja"));
        Assert.Equal(("human", "Necromancer"), Stored("Odd"));
    }

    [Fact]
    public void Down_RestoresEnumNames()
    {
        Migrator.Migrate();
        Migrator.Migrate(PreviousMigration);

        Assert.Equal(("HalfElf", "MagicUser"), Stored("Mage"));
        Assert.Equal(("Lizardman", "Ninja"), Stored("Ninja"));
        Assert.Equal(("Human", "Necromancer"), Stored("Odd"));
    }

    private void AddCharacter(Account account, string name, string race, string characterClass) =>
        _db.Characters.Add(new Character { AccountId = account.AccountId, Name = name, Race = race, CharacterClass = characterClass });

    private (string Race, string CharacterClass) Stored(string name)
    {
        _db.ChangeTracker.Clear();
        var character = _db.Characters.AsNoTracking().Single(c => c.Name == name);
        return (character.Race, character.CharacterClass);
    }
}
