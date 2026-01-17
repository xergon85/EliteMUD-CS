namespace EliteMud.Application;

public sealed class PromptCatalog
{
    public string GetWelcomeMessage()
    {
        return "Welcome to EliteMUD (rewrite in progress).";
    }

    public string GetNamePrompt()
    {
        return "Enter your name:";
    }
}
