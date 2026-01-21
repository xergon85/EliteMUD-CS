namespace EliteMud.Server;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var port = ServerBootstrap.TryParsePort(args) ?? ServerBootstrap.DefaultPort;
        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        var (server, tickService) = ServerBootstrap.CreateServer(port);
        Console.WriteLine($"EliteMUD Telnet server listening on {port}.");
        
        // Start the game tick service in background
        var tickTask = Task.Run(() => tickService.RunAsync(cancellationTokenSource.Token), cancellationTokenSource.Token);
        
        // Run the server (blocks until cancellation)
        await server.RunAsync(cancellationTokenSource.Token);
        
        // Wait for tick service to complete
        await tickTask;
    }
}
