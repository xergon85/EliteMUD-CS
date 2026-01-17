namespace EliteMud.Application;

public interface IConnectionDirectory
{
    IReadOnlyList<string> GetPlayerNames();
}
