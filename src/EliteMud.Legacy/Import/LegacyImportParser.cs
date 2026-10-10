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

        return ParseLegacyNumber(SplitGluedNumber(token));
    }

    /// <summary>
    /// Legacy reads list values with fscanf("%ld"), which stops at the first non-digit. Some
    /// files glue the list terminator onto the last value ("385-1"), so split off the remainder
    /// and push it back as the next token.
    /// </summary>
    private string SplitGluedNumber(string token)
    {
        var match = GluedNumberPattern.Match(token);
        if (!match.Success)
        {
            return token;
        }

        PushToken(match.Groups[2].Value);
        return match.Groups[1].Value;
    }

    private static readonly System.Text.RegularExpressions.Regex GluedNumberPattern =
        new(@"^(-?\d+)(-\d+)$");

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

    /// <summary>
    /// Reads a tilde-terminated string.
    /// Legacy: fread_string() in db.c:2726 reads whole lines and stops at the first line whose
    /// last non-whitespace character is '~'. Only that final '~' is removed; a '~' elsewhere is
    /// content, and nothing after the terminator on its line is kept (e.g. a stray "~~").
    /// </summary>
    public string ReadTildeString()
    {
        var builder = new System.Text.StringBuilder();
        string? line;
        while ((line = _reader.ReadLine()) is not null)
        {
            line = line.Replace("\r", string.Empty);
            var trimmedEnd = line.TrimEnd();
            if (trimmedEnd.EndsWith('~'))
            {
                builder.Append(trimmedEnd[..^1]);
                break;
            }

            builder.Append(line).Append('\n');
        }

        return builder.ToString().Trim();
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

    /// <summary>
    /// Reads dice in the form "XdY+Z".
    /// Legacy reads dice with fscanf("%ldd%ld+%ld"), which stops after Z. Some files have the
    /// next value glued on (e.g. "3d6+9-1" where "-1" ends the attack list), so any remainder
    /// is pushed back as the next token.
    /// </summary>
    public string ReadDiceString()
    {
        var token = ReadToken();
        if (token is null)
        {
            return string.Empty;
        }

        var match = DicePattern.Match(token);
        if (match.Success && match.Length < token.Length)
        {
            PushToken(token[match.Length..]);
            return match.Value;
        }

        return token;
    }

    private static readonly System.Text.RegularExpressions.Regex DicePattern =
        new(@"^\d+d\d+\+-?\d+");

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
