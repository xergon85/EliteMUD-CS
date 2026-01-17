namespace EliteMud.Application;

public sealed class CommandCatalog
{
    public string GetUnknownCommandMessage()
    {
        return "Unknown command. Try 'look', 'who', 'say', 'zreset', 'north', or 'go north'.";
    }
}
