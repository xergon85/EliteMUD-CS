using EliteMud.Game;

namespace EliteMud.Application.Progression;

/// <summary>
/// Applies experience changes to a player: the XP, and on a level-up the new maximum points, stats, level and
/// title. The rules themselves are in <see cref="ExperienceService"/>.
/// </summary>
public sealed class PlayerProgression
{
    private readonly ExperienceService _experience;
    private readonly RulesContent _rules;

    public PlayerProgression(ExperienceService experience, RulesContent rules)
    {
        _experience = experience;
        _rules = rules;
    }

    /// <summary>Add (or with a negative amount, remove) experience, levelling up when it crosses the threshold.</summary>
    public ProgressionResult GainExperience(PlayerState player, int amount)
    {
        var characterClass = Lookup(_rules.Classes, player.CharacterClass, "class", player);
        var gain = _experience.GainExperience(characterClass, player.Level, player.Experience, amount);
        player.Experience = gain.Experience;
        if (!gain.LeveledUp)
        {
            return new ProgressionResult(gain.Messages, false);
        }

        var race = Lookup(_rules.Races, player.Race, "race", player);
        var levelUp = AdvanceOneLevel(player, characterClass, race);

        player.SaveRequested = true;
        Console.WriteLine($"{player.Name} advanced to level {player.Level}");
        return new ProgressionResult(gain.Messages.Concat(levelUp.Messages).ToList(), true);
    }

    /// <summary>
    /// Set the level directly (the setlevel test command). Going up runs the real level-up once per level, so
    /// points and stats grow as if earned; going down only changes the level and title. XP resets to 0.
    /// </summary>
    public SetLevelResult SetLevel(PlayerState player, int level)
    {
        var characterClass = Lookup(_rules.Classes, player.CharacterClass, "class", player);
        var race = Lookup(_rules.Races, player.Race, "race", player);
        var from = player.Level;
        int hitPoints = 0, mana = 0, movement = 0;
        while (player.Level < level)
        {
            var levelUp = AdvanceOneLevel(player, characterClass, race);
            hitPoints += levelUp.HitPointGain;
            mana += levelUp.ManaGain;
            movement += levelUp.MovementGain;
        }

        player.Level = (byte)level;
        player.Experience = 0;
        player.Title = ExperienceService.Title(characterClass, race, (Sex)player.Sex, player.Level);
        player.SaveRequested = true;
        return new SetLevelResult(from, level, hitPoints, mana, movement);
    }

    /// <summary>
    /// The player's title, or for a character that has none yet the class title for their level, as legacy
    /// sets one at creation.
    /// </summary>
    public static string DisplayTitle(PlayerState player, RulesContent rules)
    {
        if (!string.IsNullOrEmpty(player.Title))
        {
            return player.Title;
        }

        return rules.Classes.TryGetValue(player.CharacterClass, out var characterClass)
               && rules.Races.TryGetValue(player.Race, out var race)
            ? ExperienceService.Title(characterClass, race, (Sex)player.Sex, player.Level)
            : "";
    }

    /// <summary>One level-up applied to the player: maximums, stats, level and title.</summary>
    private LevelUpResult AdvanceOneLevel(PlayerState player, ClassDefinition characterClass, RaceDefinition race)
    {
        var levelUp = _experience.AdvanceLevel(characterClass, race, player.Level, Abilities(player));

        // Legacy raises the maximums only; current points refill over time
        player.MaxHitPoints = (short)(player.MaxHitPoints + levelUp.HitPointGain);
        player.MaxMana = (short)(player.MaxMana + levelUp.ManaGain);
        player.MaxMovement = (short)(player.MaxMovement + levelUp.MovementGain);
        Apply(player, levelUp.Abilities);
        player.Level++;

        // Legacy skips this for players with NOTITLE; that flag doesn't exist yet
        player.Title = ExperienceService.Title(characterClass, race, (Sex)player.Sex, player.Level);
        return levelUp;
    }

    /// <summary>XP the player needs for the next level.</summary>
    public int ExpNeeded(PlayerState player) =>
        _experience.ExpNeeded(Lookup(_rules.Classes, player.CharacterClass, "class", player), player.Level);

    // Characters with an unknown class or race are refused at login, so this means content changed under a player
    private static T Lookup<T>(IReadOnlyDictionary<string, T> definitions, string id, string kind, PlayerState player) =>
        definitions.TryGetValue(id, out var definition)
            ? definition
            : throw new InvalidOperationException($"{player.Name} has unknown {kind} '{id}'");

    private static AbilityScores Abilities(PlayerState player) => new(
        player.Strength,
        player.StrengthAdd,
        player.Intelligence,
        player.Wisdom,
        player.Dexterity,
        player.Constitution,
        player.Charisma);

    private static void Apply(PlayerState player, AbilityScores abilities)
    {
        player.Strength = (sbyte)abilities.Strength;
        player.StrengthAdd = (sbyte)abilities.StrengthAdd;
        player.Intelligence = (sbyte)abilities.Intelligence;
        player.Wisdom = (sbyte)abilities.Wisdom;
        player.Dexterity = (sbyte)abilities.Dexterity;
        player.Constitution = (sbyte)abilities.Constitution;
        player.Charisma = (sbyte)abilities.Charisma;
    }
}
