namespace EliteMud.Legacy.Import;

internal sealed class LegacyImportParser
{
    private readonly TextReader _reader;
    private string? _bufferedToken;

    public LegacyImportParser(TextReader reader)
    {
        _reader = reader;
    }

    public string? ReadToken()
    {
        if (_bufferedToken is not null)
        {
            var token = _bufferedToken;
            _bufferedToken = null;
            return token;
        }

        var builder = new System.Text.StringBuilder();
        int next;
        do
        {
            next = _reader.Read();
            if (next == -1)
            {
                return null;
            }
        } while (char.IsWhiteSpace((char)next));

        do
        {
            builder.Append((char)next);
            next = _reader.Read();
            if (next == -1)
            {
                break;
            }
        } while (!char.IsWhiteSpace((char)next));

        return builder.ToString();
    }

    public void PushToken(string token)
    {
        _bufferedToken = token;
    }

    public int ReadNumber()
    {
        var token = ReadToken();
        if (token is null)
        {
            return 0;
        }

        return ParseLegacyNumber(token);
    }

    public bool TryReadNumber(out int value)
    {
        var token = ReadToken();
        if (token is null)
        {
            value = 0;
            return false;
        }

        if (token.StartsWith('#') || token == "$")
        {
            PushToken(token);
            value = 0;
            return false;
        }

        value = ParseLegacyNumber(token);
        return true;
    }

    public string ReadTildeString()
    {
        var builder = new System.Text.StringBuilder();
        int next;
        while ((next = _reader.Read()) != -1)
        {
            if (next == '~')
            {
                break;
            }

            if (next == '\r')
            {
                continue;
            }

            builder.Append((char)next);
        }

        return builder.ToString().TrimEnd();
    }

    public string? ReadLineSkippingWhitespace()
    {
        string? line;
        do
        {
            line = _reader.ReadLine();
            if (line is null)
            {
                return null;
            }
        } while (line.Length == 0);

        return line.TrimStart();
    }

    public string ReadProgramBlock()
    {
        var builder = new System.Text.StringBuilder();
        string? line;
        while ((line = _reader.ReadLine()) is not null)
        {
            if (line.StartsWith("~"))
            {
                break;
            }

            builder.AppendLine(line);
        }

        return builder.ToString().TrimEnd();
    }

    public string ReadDiceString()
    {
        var token = ReadToken();
        return token ?? string.Empty;
    }

    private static int ParseLegacyNumber(string token)
    {
        if (token.StartsWith("-", StringComparison.Ordinal))
        {
            return int.TryParse(token, out var value) ? value : 0;
        }

        if (token.All(char.IsDigit))
        {
            return int.TryParse(token, out var value) ? value : 0;
        }

        var parts = token.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var valueSum = 0;
        foreach (var part in parts)
        {
            var value = 0;
            foreach (var ch in part)
            {
                if (char.IsLower(ch))
                {
                    value |= 1 << (ch - 'a');
                }
                else if (char.IsUpper(ch))
                {
                    value |= 1 << (26 + (ch - 'A'));
                }
                else if (char.IsDigit(ch))
                {
                    value = value * 10 + (ch - '0');
                }
            }

            valueSum += value;
        }

        return valueSum;
    }
}
