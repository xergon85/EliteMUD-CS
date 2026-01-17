namespace EliteMud.Application.Commands.Shared;

[Obsolete("Use per-command registrations in the server layer.")]
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
