using System.Threading;
using System.Threading.Tasks;
using EliteMud.Game;

namespace EliteMud.Legacy;

public interface ILegacyWorldLoader
{
    ValueTask<WorldDefinition> LoadAsync(string legacyPath, CancellationToken cancellationToken);
}
