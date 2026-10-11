using EliteMud.Application.Formulas;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Progression;

/// <summary>
/// Experience, level-up and title rules, ported from legacy limits.c (exp_needed, gain_exp, advance_level,
/// improve_abilities, set_title).
/// Takes class and level as inputs instead of a player, so multi-class can later call it per class slot.
/// </summary>
public sealed class ExperienceService
{
    /// <summary>The highest mortal level; 110+ are immortals (legacy LEVEL_DEITY - 1).</summary>
    public const int MaxMortalLevel = 109;

    private const int ImmortalLevel = MaxMortalLevel + 1;

    // A stat improves when number(threshold, 100) < level: 0 for the class's prime stats, 25 for the others
    private const int PrimeStatThreshold = 0;
    private const int OtherStatThreshold = 25;

    // Legacy act() picks one of these: "You {rise^gain^advance} a level!"
    private static readonly string[] LevelUpVerbs = { "rise", "gain", "advance" };

    private readonly RulesContent _rules;
    private readonly FormulaEvaluator _formulas;
    private readonly Random _random;

    /// <param name="random">Pass a seeded one in tests, shared with the FormulaEvaluator. Defaults to Random.Shared.</param>
    public ExperienceService(RulesContent rules, FormulaEvaluator formulas, Random? random = null)
    {
        _rules = rules;
        _formulas = formulas;
        _random = random ?? Random.Shared;
    }

    /// <summary>XP needed to gain a level from <paramref name="level"/> (legacy exp_needed()).</summary>
    public int ExpNeeded(ClassDefinition characterClass, int level) => characterClass.XpCurve.XpNeeded(level);

    /// <summary>
    /// Add or remove experience (legacy gain_exp()). A gain is capped by the content's maxGainFormula, gives at
    /// most one level, and resets XP to 0 on a level-up. Level 109 gains no more XP; immortals are unaffected.
    /// A loss never takes XP below 0.
    /// </summary>
    public ExperienceGain GainExperience(ClassDefinition characterClass, int level, int experience, int amount)
    {
        var messages = new List<string>();
        if (level >= ImmortalLevel)
        {
            return new ExperienceGain(experience, 0, false, messages);
        }

        if (amount > 0)
        {
            if (level < 1 || level >= MaxMortalLevel)
            {
                return new ExperienceGain(experience, 0, false, messages);
            }

            var needed = ExpNeeded(characterClass, level);
            var cap = _formulas.EvaluateInt(
                _rules.Progression.MaxGainFormula, new GainCapInputs(needed, level), "progression/rules.maxGainFormula");
            var gain = Math.Min(cap, amount);
            if (experience + gain > needed)
            {
                messages.Add($"You {LevelUpVerbs[_random.Next(LevelUpVerbs.Length)]} a level!");
                return new ExperienceGain(0, gain, true, messages);
            }

            // Legacy prints "1 lousy" for any gain that isn't above 1
            messages.Add(gain > 1 ? $"You receive {gain} experience points." : "You receive 1 lousy experience point.");
            return new ExperienceGain(experience + gain, gain, false, messages);
        }

        if (amount < 0)
        {
            if (experience <= 0)
            {
                messages.Add("You have no experience to lose.");
                return new ExperienceGain(0, 0, false, messages);
            }

            // Negate as long: -int.MinValue overflows back to int.MinValue
            var loss = (int)Math.Min(-(long)amount, experience);
            messages.Add($"You lose {loss} experience point{(loss == 1 ? "" : "s")}.");
            return new ExperienceGain(experience - loss, -loss, false, messages);
        }

        return new ExperienceGain(experience, 0, false, messages);
    }

    /// <summary>
    /// The gains for advancing from <paramref name="level"/> to the next (legacy advance_level() and
    /// improve_abilities()). Hit points, mana and movement come from the class's level-up profile; each stat may
    /// improve by one, up to the race's maximum, more often for the class's prime stats.
    /// </summary>
    public LevelUpResult AdvanceLevel(ClassDefinition characterClass, RaceDefinition race, int level, AbilityScores abilities)
    {
        var profile = characterClass.LevelUpProfile;
        var name = $"level-up/{profile.Id}";
        var inputs = new LevelUpInputs(
            level,
            abilities.Strength,
            abilities.Intelligence,
            abilities.Wisdom,
            abilities.Dexterity,
            abilities.Constitution,
            abilities.Charisma);
        var hitPoints = _formulas.EvaluateInt(profile.HpGainFormula, inputs, $"{name}.hpGain");
        var mana = _formulas.EvaluateInt(profile.ManaGainFormula, inputs, $"{name}.manaGain");
        var movement = _formulas.EvaluateInt(profile.MoveGainFormula, inputs, $"{name}.moveGain");

        var (improved, messages) = ImproveAbilities(characterClass, race, level, abilities);
        return new LevelUpResult(hitPoints, mana, movement, improved, messages);
    }

    /// <summary>
    /// "the {race title name} {class title}" (legacy set_title()), with the male title for male characters and
    /// the female one otherwise. One title row per 3 levels.
    /// </summary>
    public static string Title(ClassDefinition characterClass, RaceDefinition race, Sex sex, int level)
    {
        var row = characterClass.Titles[Math.Clamp(level / 3 + 1, 0, characterClass.Titles.Count - 1)];
        return $"the {race.TitleName} {(sex == Sex.Male ? row.Male : row.Female)}";
    }

    private (AbilityScores Abilities, List<string> Messages) ImproveAbilities(
        ClassDefinition characterClass, RaceDefinition race, int level, AbilityScores abilities)
    {
        var messages = new List<string>();
        var max = race.StatMax;

        // Same order as legacy, so a seeded run matches it roll for roll
        var strength = Improve("str", abilities.Strength, max.Strength, "Your strength has improved.");
        var constitution = Improve("con", abilities.Constitution, max.Constitution, "Your constitution has improved.");
        var dexterity = Improve("dex", abilities.Dexterity, max.Dexterity, "You are more dextrous.");
        var intelligence = Improve("int", abilities.Intelligence, max.Intelligence, "Your intelligence is improved.");
        var wisdom = Improve("wis", abilities.Wisdom, max.Wisdom, "You have gained more wisdom.");
        var charisma = Improve("cha", abilities.Charisma, max.Charisma, "Your personality has improved.");

        // 18/xx strength for classes with the strength bonus. Uses strength after the roll above, as legacy does,
        // and stops at 100 (legacy could pass it, e.g. 98 + 7)
        var strengthAdd = abilities.StrengthAdd;
        if (Roll(Threshold("str")) < level
            && strength > 17
            && strengthAdd < 100
            && Roll(1) > strengthAdd
            && characterClass.StrengthBonus)
        {
            strengthAdd = Math.Min(100, strengthAdd + _random.Next(3, 8));
            messages.Add("Your strength grows.");
        }

        var improved = new AbilityScores(strength, strengthAdd, intelligence, wisdom, dexterity, constitution, charisma);
        return (improved, messages);

        int Improve(string stat, int value, int raceMax, string message)
        {
            if (Roll(Threshold(stat)) < level && value < raceMax && _random.Next(1, raceMax + 1) > value)
            {
                messages.Add(message);
                return value + 1;
            }

            return value;
        }

        int Threshold(string stat) =>
            characterClass.PrimeStats.Contains(stat) ? PrimeStatThreshold : OtherStatThreshold;

        // Legacy number(min, 100)
        int Roll(int min) => _random.Next(min, 101);
    }
}
