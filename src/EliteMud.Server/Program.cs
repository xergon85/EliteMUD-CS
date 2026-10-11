namespace EliteMud.Server;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Check the world content and exit; the exit code is the number of errors (0 = clean)
        if (args.Contains("--validate"))
        {
            return Math.Min(ServerBootstrap.ValidateContent(), 125);
        }

        var port = ServerBootstrap.TryParsePort(args) ?? ServerBootstrap.DefaultPort;
        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        TelnetServer server;
        GameTickService tickService;
        try
        {
            (server, tickService) = ServerBootstrap.CreateServer(port);
        }
        catch (ContentConfigurationException ex)
        {
            // Fail fast with a clear message instead of a stack trace
            Console.Error.WriteLine($"Server not started: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"EliteMUD Telnet server listening on {port}.");
        
        // Start the game tick service in background
        var tickTask = Task.Run(() => tickService.RunAsync(cancellationTokenSource.Token), cancellationTokenSource.Token);
        
        // Run the server (blocks until cancellation)
        await server.RunAsync(cancellationTokenSource.Token);
        
        // Wait for tick service to complete
        await tickTask;
        return 0;
    }
}
