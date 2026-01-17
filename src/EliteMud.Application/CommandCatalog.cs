namespace EliteMud.Application;

public sealed class CommandCatalog
{
    private const string HelpText = "look, who, say, zreset, north, go north";

    public string GetUnknownCommandMessage()
    {
        return $"Unknown command. Try {HelpText}.";
    }

    public string GetHelpText()
    {
        return HelpText;
    }

    public string GetResetUsage()
    {
        return "Usage: zreset [zoneId]";
    }
}
