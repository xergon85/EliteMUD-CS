using EliteMud.Game;

namespace EliteMud.Application.Session;

public sealed class PromptCatalog
{
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
    
    public string GetRacePrompt()
    {
        return @"
Select a race:
  [a] Human       [b] Troll       [c] Halfling
  [d] Dwarf       [e] Gnome       [f] Elf
  [g] Half-elf    [h] Fairy       [i] Minotaur
  [j] Ratman      [k] Drow        [l] Lizardman
  [m] Draconian

Enter capital letter to get info about race
Race: ";
    }
    
    public string GetClassPrompt(List<CharacterClass> allowedClasses)
    {
        var prompt = "\nSelect Class:\n";
        
        var classOptions = new Dictionary<char, CharacterClass>
        {
            { 'a', CharacterClass.MagicUser },
            { 'b', CharacterClass.Cleric },
            { 'c', CharacterClass.Thief },
            { 'd', CharacterClass.Warrior },
            { 'e', CharacterClass.Psionicist },
            { 'f', CharacterClass.Monk },
            { 'g', CharacterClass.Bard },
            { 'h', CharacterClass.Knight },
            { 'i', CharacterClass.Wizard },
            { 'j', CharacterClass.Druid },
            { 'k', CharacterClass.Assassin },
            { 'l', CharacterClass.Ranger },
            { 'm', CharacterClass.Illusionist },
            { 'n', CharacterClass.Paladin },
            { 'o', CharacterClass.Mariner },
            { 'p', CharacterClass.Cavalier },
            { 's', CharacterClass.Ninja }
        };
        
        foreach (var kvp in classOptions)
        {
            if (allowedClasses.Contains(kvp.Value))
            {
                prompt += $"  [{kvp.Key}] {ClassNames.Names[kvp.Value]}\n";
            }
        }
        
        prompt += "\nEnter [-] to go back to race selection\n";
        prompt += "Enter capital letter to get info about class\n";
        prompt += "Class: ";
        
        return prompt;
    }
    
    public string GetCharacterSelectionMenu(List<CharacterListItem> characters)
    {
        var menu = "\n╔════════════════════════════════════════════════════════════╗\n";
        menu += "║                    Your Characters                         ║\n";
        menu += "╚════════════════════════════════════════════════════════════╝\n\n";
        
        for (int i = 0; i < characters.Count; i++)
        {
            var ch = characters[i];
            menu += $"  [{i + 1}] {ch.Name} (Level {ch.Level} {ch.CharacterClass})\n";
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
