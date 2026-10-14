namespace EliteMud.Game;

/// <summary>
/// Legacy str_app (constants.c:2492): what strength adds to hit and to damage. Rows 0–25 are strength itself;
/// 26–30 are 18 with an exceptional bonus (18/01–18/50, /51–/75, /76–/90, /91–/99, 18/00). Legacy's carry, wield and
/// bash columns aren't used here yet.
/// </summary>
public static class StrengthApply
{
    private static readonly (int ToHit, int ToDamage)[] Table =
    {
        (-5, -4), (-5, -4), (-3, -2), (-3, -1), (-2, -1), (-2, -1), // 0–5
        (-1, 0), (-1, 0), (0, 0), (0, 0), (0, 0), (0, 0), // 6–11
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 1), (1, 1), // 12–17
        (1, 2), (3, 7), (3, 8), (4, 9), (4, 10), (5, 11), // 18–23
        (6, 12), (7, 14), // 24–25
        (1, 3), (2, 3), (2, 4), (2, 5), (3, 6) // 18/01–18/50, /51–/75, /76–/90, /91–/99, 18/00
    };

    public static int ToHit(int strength, int strengthAdd = 0) => Table[Index(strength, strengthAdd)].ToHit;

    public static int ToDamage(int strength, int strengthAdd = 0) => Table[Index(strength, strengthAdd)].ToDamage;

    /// <summary>Legacy STRENGTH_APPLY_INDEX: the bonus only counts at exactly 18 (utils.h:225).</summary>
    private static int Index(int strength, int strengthAdd)
    {
        if (strength != 18 || strengthAdd <= 0)
        {
            return Math.Clamp(strength, 0, 25);
        }

        return strengthAdd switch
        {
            <= 50 => 26,
            <= 75 => 27,
            <= 90 => 28,
            <= 99 => 29,
            _ => 30
        };
    }
}
