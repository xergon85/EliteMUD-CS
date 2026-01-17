namespace EliteMud.Application.Commands.Shared;

public sealed class CommandRegistry
{
    private static readonly CommandKind[] DefaultCommands =
    {
        CommandKind.None,
        CommandKind.Quit,
        CommandKind.Look,
        CommandKind.Who,
        CommandKind.ResetZone,
        CommandKind.Say,
        CommandKind.Move
    };

    public IReadOnlyList<CommandKind> Commands => DefaultCommands;
}
