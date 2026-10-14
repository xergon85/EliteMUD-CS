using System.Threading.Channels;
using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Data;
using EliteMud.Game;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server;

/// <summary>
/// Background service that processes character save requests in a queue.
/// Ensures all saves use the same DbContext scope, preventing concurrency conflicts.
/// </summary>
internal sealed class CharacterSaveQueue : IDisposable
{
    private readonly Channel<SaveRequest> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorldState _worldState;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts;
    private readonly GameStateGate _gate;

    public CharacterSaveQueue(IServiceProvider serviceProvider, IWorldState worldState, GameStateGate gate)
    {
        _gate = gate;
        _serviceProvider = serviceProvider;
        _worldState = worldState;
        _channel = Channel.CreateUnbounded<SaveRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _cts = new CancellationTokenSource();
        _processingTask = Task.Run(() => ProcessSavesAsync(_cts.Token));
    }

    /// <summary>
    /// Queue a character save operation. Returns immediately without blocking.
    /// </summary>
    public async ValueTask QueueSaveAsync(int characterId, PlayerState playerState, CancellationToken cancellationToken = default)
    {
        var request = new SaveRequest(characterId, playerState, DateTime.UtcNow);
        await _channel.Writer.WriteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Queue a character save and wait for it to complete.
    /// </summary>
    public async Task<bool> QueueSaveAndWaitAsync(int characterId, PlayerState playerState, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        var request = new SaveRequest(characterId, playerState, DateTime.UtcNow, tcs);
        await _channel.Writer.WriteAsync(request, cancellationToken);
        return await tcs.Task;
    }

    private async Task ProcessSavesAsync(CancellationToken cancellationToken)
    {
        // When the last successful save of each character started (deduplication)
        var lastSaveStart = new Dictionary<int, DateTime>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var request = await _channel.Reader.ReadAsync(cancellationToken);

                // Deduplicate: a save reads the live PlayerState, so one that started after this request was made
                // already stored everything the request wanted. Anything older could miss changes, so save again.
                if (lastSaveStart.TryGetValue(request.CharacterId, out var lastStart) && lastStart >= request.RequestTime)
                {
                    Console.WriteLine($"[SaveQueue] Skipping save for character {request.CharacterId}: a later save already covered it");
                    request.Complete(true);
                    continue;
                }

                // Process the save in a new scope
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();

                try
                {
                    var character = await repository.GetByIdAsync(request.CharacterId, cancellationToken);
                    if (character != null)
                    {
                        // Read the live player under the gate (#61), so the game isn't changing it mid-copy;
                        // the database work stays outside
                        DateTime saveStart;
                        using (await _gate.EnterAsync(cancellationToken))
                        {
                            saveStart = DateTime.UtcNow;
                            CharacterMapper.UpdateCharacterFromPlayerState(character, request.PlayerState, _worldState);
                        }

                        await repository.UpdateAsync(character, cancellationToken);
                        
                        lastSaveStart[request.CharacterId] = saveStart;
                        request.Complete(true);
                    }
                    else
                    {
                        Console.WriteLine($"[SaveQueue] Character {request.CharacterId} not found in database");
                        request.Complete(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SaveQueue] Error saving character {request.CharacterId}: {ex.Message}");
                    request.Complete(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ChannelClosedException)
            {
                // Dispose() completed the channel: shut down without faulting the processing task
                break;
            }
        }
    }

    public void Dispose()
    {
        _channel.Writer.Complete();
        _cts.Cancel();
        _processingTask.Wait(TimeSpan.FromSeconds(5));
        _cts.Dispose();
    }

    private record SaveRequest(
        int CharacterId, 
        PlayerState PlayerState, 
        DateTime RequestTime,
        TaskCompletionSource<bool>? CompletionSource = null)
    {
        public void Complete(bool success)
        {
            CompletionSource?.TrySetResult(success);
        }
    }
}
