namespace EliteMud.Application.Commands.Who;

public interface IConnectionDirectory
{
    IReadOnlyList<string> GetPlayerNames();
}
