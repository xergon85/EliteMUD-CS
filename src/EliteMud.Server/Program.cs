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

        var server = ServerBootstrap.CreateServer(port);
        Console.WriteLine($"EliteMUD Telnet server listening on {port}.");
        await server.RunAsync(cancellationTokenSource.Token);
    }
}
