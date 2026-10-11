namespace EliteMud.Server;

/// <summary>
/// Content that the server refuses to start with (e.g. a broken formula). The message says what to fix.
/// </summary>
internal sealed class ContentConfigurationException : Exception
{
    public ContentConfigurationException(string message)
        : base(message)
    {
    }
}
