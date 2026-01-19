using System.Buffers;
using System.Net.Sockets;
using System.Text;

namespace EliteMud.Server;

internal sealed class TelnetSession
{
    private const byte TelnetIac = 255;
    private const byte TelnetCommandLength = 3;

    private readonly NetworkStream _stream;
    private readonly Encoding _encoding = Encoding.ASCII;

    public TelnetSession(NetworkStream stream)
    {
        _stream = stream;
    }

    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            var builder = new StringBuilder();
            while (true)
            {
                var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    return null;
                }

                var cursor = 0;
                while (cursor < bytesRead)
                {
                    var current = buffer[cursor];
                    if (current == TelnetIac)
                    {
                        cursor = SkipTelnetCommand(cursor, bytesRead);
                        continue;
                    }

                    if (current == '\n')
                    {
                        return builder.ToString().TrimEnd('\r');
                    }

                    builder.Append((char)current);
                    cursor++;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async ValueTask SendLineAsync(string message, CancellationToken cancellationToken)
    {
        // Translate color codes (#X format) to ANSI escape sequences
        var translatedMessage = ColorTranslator.TranslateColors(message);
        var payload = _encoding.GetBytes(translatedMessage + "\r\n");
        await _stream.WriteAsync(payload, cancellationToken);
    }

    public async ValueTask SendAsync(string message, CancellationToken cancellationToken)
    {
        // Translate color codes (#X format) to ANSI escape sequences
        var translatedMessage = ColorTranslator.TranslateColors(message);
        var payload = _encoding.GetBytes(translatedMessage);
        await _stream.WriteAsync(payload, cancellationToken);
    }

    private static int SkipTelnetCommand(int cursor, int bytesRead)
    {
        var next = cursor + TelnetCommandLength;
        return next <= bytesRead ? next : bytesRead;
    }
}
