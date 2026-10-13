namespace EliteMud.Application.Progression;

/// <summary>
/// What a new level 1 character starts with. Used by character creation and by setlevel's rebuild, so they can't
/// drift apart. #25 replaces the fixed points with the race's starting values (legacy hp_start, mn_start, mv_start).
/// </summary>
public static class CharacterStart
{
    /// <summary>Legacy mortal_start_room, the Temple of Midgaard: start, recall, respawn and fallback room.</summary>
    public const int StartRoom = 3001;

    /// <summary>Every stat starts here; stat rolling is deferred (see the levelling plan).</summary>
    public const int Stat = 11;

    public const int HitPoints = 20;
    public const int Mana = 100;
    public const int Movement = 100;
}
