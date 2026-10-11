using System.Text;
using EliteMud.Game;

namespace EliteMud.Application.Session;

public sealed class PromptCatalog
{
    private readonly RulesContent _rules;

    public PromptCatalog(RulesContent rules)
    {
        _rules = rules;
    }

    public string GetWelcomeMessage()
    {
        return @"
╔════════════════════════════════════════════════════════════╗
║                                                            ║
║                  Welcome to EliteMUD                       ║
║                                                            ║
║            A Classic Multi-User Dungeon                    ║
║                                                            ║
╚════════════════════════════════════════════════════════════╝
";
    }

    public string GetAccountNamePrompt()
    {
        return "Account name: ";
    }
    
    public string GetPasswordPrompt()
    {
        return "Password: ";
    }
    
    public string GetNewPasswordPrompt()
    {
        return "New account. Choose a password: ";
    }
    
    public string GetConfirmPasswordPrompt()
    {
        return "Confirm password: ";
    }
    
    public string GetConfirmAccountNamePrompt(string name)
    {
        return $"Did I get that right, {name}? (Y/N): ";
    }
    
    public string GetCharacterNamePrompt()
    {
        return "Character name: ";
    }
    
    public string GetConfirmCharacterNamePrompt(string name)
    {
        return $"Is '{name}' correct? (Y/N): ";
    }
    
    public string GetSexPrompt()
    {
        return "What is your sex? (M/F): ";
    }
    
    /// <summary>The creation races from content, three per row, with their legacy menu letters.</summary>
    public string GetRacePrompt()
    {
        var prompt = new StringBuilder("\nSelect a race:\n");
        foreach (var row in _rules.CreationRaces.Chunk(3))
        {
            var entries = row.Select(race => $"[{race.MenuKey}] {race.Name}".PadRight(16));
            prompt.Append("  ").Append(string.Concat(entries).TrimEnd()).Append('\n');
        }

        prompt.Append("\nEnter capital letter to get info about race\nRace: ");
        return prompt.ToString();
    }

    /// <summary>The race's allowed classes, by menu letter.</summary>
    public string GetClassPrompt(RaceDefinition race)
    {
        var prompt = new StringBuilder("\nSelect Class:\n");
        foreach (var characterClass in race.AllowedClasses.OrderBy(c => c.MenuKey, StringComparer.Ordinal))
        {
            prompt.Append($"  [{characterClass.MenuKey}] {characterClass.Name}\n");
        }

        prompt.Append("\nEnter [-] to go back to race selection\n");
        prompt.Append("Enter capital letter to get info about class\n");
        prompt.Append("Class: ");
        return prompt.ToString();
    }
    
    public string GetCharacterSelectionMenu(List<CharacterListItem> characters)
    {
        var menu = "\n╔════════════════════════════════════════════════════════════╗\n";
        menu += "║                    Your Characters                         ║\n";
        menu += "╚════════════════════════════════════════════════════════════╝\n\n";
        
        for (int i = 0; i < characters.Count; i++)
        {
            var ch = characters[i];
            menu += $"  [{i + 1}] {ch.Name} (Level {ch.Level} {_rules.ClassName(ch.CharacterClass)})\n";
        }
        
        menu += $"\n  [N] Create new character\n";
        menu += $"  [D] Delete a character\n";
        menu += $"  [Q] Quit\n\n";
        menu += "Your choice: ";
        
        return menu;
    }
    
    public string GetMainMenu()
    {
        return @"
╔════════════════════════════════════════════════════════════╗
║                       Main Menu                            ║
╚════════════════════════════════════════════════════════════╝

  [1] Enter the game
  [0] Quit

Your choice: ";
    }
    
    public string GetMotd()
    {
        return @"
╔════════════════════════════════════════════════════════════╗
║                 Message of the Day                         ║
╚════════════════════════════════════════════════════════════╝

Welcome to EliteMUD!

This is a rewrite of the classic EliteMUD codebase in C#.
The game is currently under development.

Have fun and happy adventuring!

*** PRESS RETURN ***
";
    }
    
    public string GetGoodbyeMessage()
    {
        return "\nFarewell, adventurer!\n\n";
    }
    
    public string GetInvalidOption()
    {
        return "\nInvalid option. Please try again.\n";
    }
    
    public string GetPasswordMismatch()
    {
        return "\nPasswords don't match. Please try again.\n";
    }
    
    public string GetInvalidPassword()
    {
        return "\nInvalid password. Please try again.\n";
    }
    
    public string GetInvalidPasswordWithAttempts(int remainingAttempts)
    {
        return $"\nInvalid password. {remainingAttempts} attempt(s) remaining.\n";
    }
    
    public string GetTooManyFailedAttempts()
    {
        return "\nToo many failed password attempts. Disconnecting...\n";
    }
    
    public string GetAccountCreated()
    {
        return "\nAccount created successfully!\n";
    }
    
    public string GetCharacterCreated()
    {
        return "\nCharacter created successfully!\n";
    }
    
    public string GetCharacterLimitReached(int limit)
    {
        return $"\nYou have reached the maximum number of characters ({limit}).\nPlease delete a character before creating a new one.\n";
    }
}
