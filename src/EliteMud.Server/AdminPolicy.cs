using System.Text.Json;

namespace EliteMud.Server;

/// <summary>
/// Who may use admin commands, until permission levels exist (#11): the accounts listed in elitemud.config.json
/// next to the database. No file means no admins.
/// </summary>
internal sealed class AdminPolicy
{
    public const string ConfigFileName = "elitemud.config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly HashSet<string> _adminAccounts;

    public AdminPolicy(IEnumerable<string> adminAccounts)
    {
        // Exact match, as logins identify accounts today: "Xergon" is a different account from "xergon", so
        // ignoring case would let anyone register a look-alike and get admin. Revisit with #46.
        _adminAccounts = new HashSet<string>(adminAccounts, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> AdminAccounts => _adminAccounts;

    public bool IsAdmin(ConnectionContext context) => _adminAccounts.Contains(context.AccountName);

    /// <summary>
    /// Read the admin accounts from <paramref name="path"/>. A missing file gives no admins; a file that can't be
    /// read stops the server, so a typo can't silently lock everyone out or in.
    /// </summary>
    public static AdminPolicy Load(string path)
    {
        // Read directly instead of checking File.Exists first: an existing file we can't read must be an error,
        // not quietly treated as missing
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            Console.WriteLine($"No {ConfigFileName} found; admin commands are disabled for everyone.");
            return new AdminPolicy(Array.Empty<string>());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ContentConfigurationException($"{ConfigFileName} can't be read: {ex.Message}");
        }

        ServerConfigFile? file;
        try
        {
            file = JsonSerializer.Deserialize<ServerConfigFile>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ContentConfigurationException($"{ConfigFileName}: {ex.Message}");
        }

        var accounts = (file?.AdminAccounts ?? new List<string>())
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .ToList();
        Console.WriteLine(accounts.Count == 0
            ? $"{ConfigFileName}: no admin accounts listed; admin commands are disabled for everyone."
            : $"{ConfigFileName}: admin accounts: {string.Join(", ", accounts)}");
        return new AdminPolicy(accounts);
    }

    private sealed class ServerConfigFile
    {
        public List<string>? AdminAccounts { get; set; }
    }
}
