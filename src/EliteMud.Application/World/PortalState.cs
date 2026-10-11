namespace EliteMud.Application.World;

/// <summary>
/// Runtime state of a portal object.
/// Legacy portal values (act.movement.c enter_portal):
/// [0] destination, [1] state flags, [2] key object (-1 = none),
/// [3] min level (-1 = none), [4] max level (-1 = none),
/// [5] mud hours before an open portal closes again (-1 = never)
/// </summary>
public sealed class PortalState
{
    // Legacy: PORTAL_CLOSED / PORTAL_LOCKED in structs.h
    public const int ClosedFlag = 1 << 0;
    public const int LockedFlag = 1 << 1;

    private const int None = -1;

    private PortalState(int destination, int flags, int keyObjectId, int minLevel, int maxLevel, int defaultHoursOpen)
    {
        Destination = destination;
        Flags = flags;
        KeyObjectId = keyObjectId;
        MinLevel = minLevel;
        MaxLevel = maxLevel;
        DefaultHoursOpen = defaultHoursOpen;
        HoursOpenRemaining = defaultHoursOpen;
    }

    public int Destination { get; set; }

    public int Flags { get; set; }

    /// <summary>Object vnum needed to activate the portal, or -1 for none.</summary>
    public int KeyObjectId { get; set; }

    public int MinLevel { get; }

    public int MaxLevel { get; }

    /// <summary>Hours an activated portal stays open, from the object prototype (-1 = forever).</summary>
    public int DefaultHoursOpen { get; }

    public int HoursOpenRemaining { get; set; }

    public bool IsClosed
    {
        get => (Flags & ClosedFlag) != 0;
        set => Flags = value ? Flags | ClosedFlag : Flags & ~ClosedFlag;
    }

    public bool IsLocked => (Flags & LockedFlag) != 0;

    public bool HasKey => KeyObjectId != None;

    public bool IsTooLowLevel(int level) => MinLevel != None && MinLevel > level;

    public bool IsTooHighLevel(int level) => MaxLevel != None && MaxLevel < level;

    public static PortalState FromValues(IReadOnlyList<int> values)
    {
        int Value(int index) => index < values.Count ? values[index] : None;

        return new PortalState(
            destination: Value(0),
            flags: Math.Max(0, Value(1)),
            keyObjectId: Value(2),
            minLevel: Value(3),
            maxLevel: Value(4),
            defaultHoursOpen: Value(5));
    }

    /// <summary>
    /// Advances the open timer by one mud hour.
    /// Legacy: point_update() in limits.c:631. Returns true when the portal closes.
    /// </summary>
    public bool TickHour()
    {
        if (!IsClosed && HoursOpenRemaining > 0)
        {
            HoursOpenRemaining--;
        }

        if (HoursOpenRemaining != 0)
        {
            return false;
        }

        var wasOpen = !IsClosed;
        IsClosed = true;
        HoursOpenRemaining = DefaultHoursOpen;
        return wasOpen;
    }
}
