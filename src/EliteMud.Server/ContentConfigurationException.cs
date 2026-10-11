namespace EliteMud.Server;

/// <summary>
/// Content or configuration that the server refuses to start with (e.g. a broken formula, an unreadable
/// elitemud.config.json). The message says what to fix.
/// </summary>
internal sealed class ContentConfigurationException : Exception
{
    public ContentConfigurationException(string message)
        : base(message)
    {
    }
}
