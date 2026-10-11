using System.Reflection;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Tests.Server;

/// <summary>
/// Admin commands (#3): only accounts listed in elitemud.config.json can use them; everyone else gets the
/// unknown-command reply, as if they didn't exist.
/// </summary>
public sealed class AdminCommandTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"elitemud-config-{Guid.NewGuid():N}");

    public AdminCommandTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void ExactlyTheAdminAndTestCommands_AreMarkedAdmin()
    {
        var adminCommands = typeof(CommandRouter).Assembly.GetTypes()
            .Concat(typeof(CommandAttribute).Assembly.GetTypes())
            .Select(type => type.GetCustomAttribute<CommandAttribute>())
            .Where(attribute => attribute is { Admin: true })
            .Select(attribute => attribute!.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(new[] { "goto", "load", "setlevel", "setskill", "setspell", "zreset" }, adminCommands);
    }

    [Theory]
    [InlineData("secret")]
    [InlineData("sec")] // aliases are admin too
    public async Task NonAdmin_GetsTheUnknownCommandReply(string verb)
    {
        var secret = new SecretHandler();
        var router = new CommandRouter(new ICommandHandler[] { secret, new OpenHandler() }, new AdminPolicy(new[] { "xergon" }));

        var outcome = await router.HandleAsync(new CommandRequest(verb, null, null), Context("player"), CancellationToken.None);

        Assert.Equal(CommandOutcome.Unknown, outcome);
        Assert.False(secret.Ran);
    }

    [Fact]
    public async Task Admin_CanUseAdminCommands()
    {
        var secret = new SecretHandler();
        var router = new CommandRouter(new ICommandHandler[] { secret }, new AdminPolicy(new[] { "xergon" }));

        var outcome = await router.HandleAsync(new CommandRequest("secret", null, null), Context("xergon"), CancellationToken.None);

        Assert.Equal(CommandOutcome.Continue, outcome);
        Assert.True(secret.Ran);
    }

    [Fact]
    public async Task LookAlikeAccount_IsNotAdmin()
    {
        // Accounts are told apart by case today (#46): "Xergon" is someone else's account, not the admin's
        var secret = new SecretHandler();
        var router = new CommandRouter(new ICommandHandler[] { secret }, new AdminPolicy(new[] { "xergon" }));

        var outcome = await router.HandleAsync(new CommandRequest("secret", null, null), Context("Xergon"), CancellationToken.None);

        Assert.Equal(CommandOutcome.Unknown, outcome);
        Assert.False(secret.Ran);
    }

    [Fact]
    public async Task OtherCommands_WorkForEveryone()
    {
        var router = new CommandRouter(new ICommandHandler[] { new OpenHandler() }, new AdminPolicy(Array.Empty<string>()));

        var outcome = await router.HandleAsync(new CommandRequest("open", null, null), Context("player"), CancellationToken.None);

        Assert.Equal(CommandOutcome.Continue, outcome);
    }

    [Fact]
    public void MissingConfig_MeansNoAdmins()
    {
        var policy = AdminPolicy.Load(Path.Combine(_directory, AdminPolicy.ConfigFileName));

        Assert.Empty(policy.AdminAccounts);
    }

    [Fact]
    public void Config_ListsAdminAccounts_WithCommentsAllowed()
    {
        var path = Write("""
            {
              // test admins
              "adminAccounts": [ "xergon", " other " ]
            }
            """);

        var policy = AdminPolicy.Load(path);

        Assert.True(policy.IsAdmin(Context("xergon")));
        Assert.True(policy.IsAdmin(Context("other"))); // surrounding spaces in the file are trimmed
        Assert.False(policy.IsAdmin(Context("player")));
    }

    [Fact]
    public void UnreadableConfig_StopsTheServer()
    {
        var path = Write("{ \"adminAccounts\": [ \"xergon\" ");

        var error = Assert.Throws<ContentConfigurationException>(() => AdminPolicy.Load(path));

        Assert.StartsWith(AdminPolicy.ConfigFileName, error.Message);
    }

    [Fact]
    public void MissingDirectory_MeansNoAdmins()
    {
        var policy = AdminPolicy.Load(Path.Combine(_directory, "nowhere", AdminPolicy.ConfigFileName));

        Assert.Empty(policy.AdminAccounts);
    }

    [Fact]
    public void ConfigThatIsADirectory_StopsTheServerWithAClearError()
    {
        // An existing path that can't be read as a file (like a permissions error) must not count as "missing"
        var path = Path.Combine(_directory, AdminPolicy.ConfigFileName);
        Directory.CreateDirectory(path);

        var error = Assert.Throws<ContentConfigurationException>(() => AdminPolicy.Load(path));

        Assert.StartsWith($"{AdminPolicy.ConfigFileName} can't be read", error.Message);
    }

    [Fact]
    public void ExampleConfig_IsValid()
    {
        var example = Path.Combine(RealWorld.RepositoryRoot, "elitemud.config.example.json");

        Assert.Equal(new[] { "your-account-name" }, AdminPolicy.Load(example).AdminAccounts);
    }

    private string Write(string json)
    {
        var path = Path.Combine(_directory, AdminPolicy.ConfigFileName);
        File.WriteAllText(path, json);
        return path;
    }

    // The gate runs before any output, so these contexts never need a real telnet session
    private static ConnectionContext Context(string account) =>
        new(1, null!, new PlayerState(1, "Tester", 3001), characterId: 1, accountName: account);

    [Command("secret", Aliases = new[] { "sec" }, Admin = true)]
    private sealed class SecretHandler : ICommandHandler
    {
        public bool Ran { get; private set; }

        public ValueTask<CommandOutcome> HandleAsync(CommandRequest command, ConnectionContext context, CancellationToken cancellationToken)
        {
            Ran = true;
            return ValueTask.FromResult(CommandOutcome.Continue);
        }
    }

    [Command("open")]
    private sealed class OpenHandler : ICommandHandler
    {
        public ValueTask<CommandOutcome> HandleAsync(CommandRequest command, ConnectionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(CommandOutcome.Continue);
    }
}
