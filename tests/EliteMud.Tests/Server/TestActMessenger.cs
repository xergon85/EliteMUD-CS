using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Tests.Server;

internal static class TestActMessenger
{
    /// <summary>An act messenger that sends down the registered test connections.</summary>
    public static ActMessenger For(IWorldState world, ConnectionRegistry connections) =>
        new(new ActMessageService(), world, new SessionCharacterOutput(connections));
}
