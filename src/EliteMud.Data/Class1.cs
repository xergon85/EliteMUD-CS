using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EliteMud.Game;

namespace EliteMud.Data;

public interface ISqliteConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}

public interface IWorldRepository
{
    ValueTask<WorldDefinition?> LoadWorldAsync(CancellationToken cancellationToken);
    ValueTask SaveWorldAsync(WorldDefinition world, CancellationToken cancellationToken);
}

public interface IScriptRepository
{
    ValueTask<IReadOnlyList<ScriptDefinition>> LoadScriptsAsync(CancellationToken cancellationToken);
    ValueTask SaveScriptAsync(ScriptDefinition script, CancellationToken cancellationToken);
}
