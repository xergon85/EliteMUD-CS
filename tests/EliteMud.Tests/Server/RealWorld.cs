using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;

namespace EliteMud.Tests.Server;

/// <summary>
/// The real zones/ data, loaded and reset like server startup does. Loaded once and shared.
/// </summary>
internal static class RealWorld
{
    private static readonly Lazy<WorldState> Instance = new(Load);

    public static WorldState State => Instance.Value;

    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EliteMUD.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    private static WorldState Load()
    {
        var (world, mobs, objects, zones) = ContentLoader.LoadFromZoneFiles(Path.Combine(RepositoryRoot, "zones"));
        if (world is null)
        {
            throw new InvalidOperationException("No zone content found.");
        }

        var worldState = new WorldState(
            world,
            mobs.ToDictionary(mob => mob.Id),
            objects.GroupBy(obj => obj.Id).ToDictionary(group => group.Key, group => group.First()),
            world.Rooms.Keys.ToDictionary(id => id, _ => new List<MobInstance>()),
            world.Rooms.Keys.ToDictionary(id => id, _ => new List<ObjectInstance>()),
            zones);
        worldState.ResetAllZones();
        return worldState;
    }
}
