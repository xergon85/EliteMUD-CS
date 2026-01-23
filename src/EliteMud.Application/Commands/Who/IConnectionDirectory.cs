using EliteMud.Game;

namespace EliteMud.Application.Commands.Who;

public interface IConnectionDirectory
{
    IReadOnlyList<PlayerState> GetPlayers();
}
