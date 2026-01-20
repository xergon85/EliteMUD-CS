using System.Collections.Concurrent;

namespace EliteMud.Application.Session;

/// <summary>
/// Tracks and manages IP-based bans for failed login attempts.
/// Thread-safe implementation using concurrent collections.
/// </summary>
public class IpBanService
{
    private readonly ConcurrentDictionary<string, DateTime> _bannedIps = new();
    private readonly ConcurrentDictionary<string, int> _failedAttempts = new();
    private readonly TimeSpan _banDuration;
    private readonly int _maxFailedAttempts;

    /// <summary>
    /// Creates a new IP ban service.
    /// </summary>
    /// <param name="banDurationMinutes">How long to ban an IP after max failed attempts (default: 15 minutes)</param>
    /// <param name="maxFailedAttempts">Maximum failed attempts before ban (default: 3)</param>
    public IpBanService(int banDurationMinutes = 15, int maxFailedAttempts = 3)
    {
        _banDuration = TimeSpan.FromMinutes(banDurationMinutes);
        _maxFailedAttempts = maxFailedAttempts;
    }

    /// <summary>
    /// Checks if an IP address is currently banned.
    /// Automatically removes expired bans.
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <returns>True if banned, false if allowed</returns>
    public bool IsBanned(string ipAddress)
    {
        if (_bannedIps.TryGetValue(ipAddress, out var banExpiry))
        {
            if (DateTime.UtcNow < banExpiry)
            {
                return true; // Still banned
            }

            // Ban expired - remove it
            _bannedIps.TryRemove(ipAddress, out _);
            _failedAttempts.TryRemove(ipAddress, out _);
        }

        return false;
    }

    /// <summary>
    /// Gets the remaining ban time for an IP address.
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <returns>Remaining ban time, or null if not banned</returns>
    public TimeSpan? GetRemainingBanTime(string ipAddress)
    {
        if (_bannedIps.TryGetValue(ipAddress, out var banExpiry))
        {
            var remaining = banExpiry - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                return remaining;
            }

            // Ban expired
            _bannedIps.TryRemove(ipAddress, out _);
            _failedAttempts.TryRemove(ipAddress, out _);
        }

        return null;
    }

    /// <summary>
    /// Records a failed login attempt from an IP address.
    /// Automatically bans the IP if max attempts exceeded.
    /// </summary>
    /// <param name="ipAddress">The IP address that failed login</param>
    /// <returns>True if the IP was banned as a result of this attempt</returns>
    public bool RecordFailedAttempt(string ipAddress)
    {
        var attempts = _failedAttempts.AddOrUpdate(ipAddress, 1, (_, count) => count + 1);

        if (attempts >= _maxFailedAttempts)
        {
            // Ban the IP
            var banExpiry = DateTime.UtcNow.Add(_banDuration);
            _bannedIps[ipAddress] = banExpiry;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears failed attempts for an IP address (called on successful login).
    /// </summary>
    /// <param name="ipAddress">The IP address to clear</param>
    public void ClearFailedAttempts(string ipAddress)
    {
        _failedAttempts.TryRemove(ipAddress, out _);
    }

    /// <summary>
    /// Manually bans an IP address for the configured duration.
    /// </summary>
    /// <param name="ipAddress">The IP address to ban</param>
    public void BanIp(string ipAddress)
    {
        var banExpiry = DateTime.UtcNow.Add(_banDuration);
        _bannedIps[ipAddress] = banExpiry;
    }

    /// <summary>
    /// Manually unbans an IP address.
    /// </summary>
    /// <param name="ipAddress">The IP address to unban</param>
    public void UnbanIp(string ipAddress)
    {
        _bannedIps.TryRemove(ipAddress, out _);
        _failedAttempts.TryRemove(ipAddress, out _);
    }

    /// <summary>
    /// Gets the current failed attempt count for an IP.
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <returns>Number of failed attempts</returns>
    public int GetFailedAttemptCount(string ipAddress)
    {
        return _failedAttempts.TryGetValue(ipAddress, out var count) ? count : 0;
    }

    /// <summary>
    /// Cleans up expired bans. Should be called periodically.
    /// </summary>
    public void CleanupExpiredBans()
    {
        var now = DateTime.UtcNow;
        var expiredIps = _bannedIps
            .Where(kvp => kvp.Value < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var ip in expiredIps)
        {
            _bannedIps.TryRemove(ip, out _);
            _failedAttempts.TryRemove(ip, out _);
        }
    }
}
