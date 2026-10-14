namespace EliteMud.Server;

/// <summary>
/// Lets one thing at a time change game state (#61): a player's command, a combat tick, a player joining or
/// leaving, or a save reading a player. Legacy is single-threaded; here sessions and the tick run concurrently,
/// so each takes the gate first. Sending is safe inside it (output is queued, see <see cref="TelnetSession"/>),
/// database work is not: do it outside. Not re-entrant: never wait for the gate while holding it.
/// </summary>
internal sealed class GameStateGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>Wait for the gate; dispose the result to let the next one in.</summary>
    public async Task<Releaser> EnterAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    public readonly struct Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        internal Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose() => _semaphore.Release();
    }
}
