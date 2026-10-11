using EliteMud.Data;
using EliteMud.Data.Entities;
using EliteMud.Game;

namespace EliteMud.Application.Session.Authentication;

/// <summary>
/// Handles the complete authentication and character creation flow
/// Manages state transitions from login through character selection/creation
/// </summary>
public class AuthenticationHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly IPasswordService _passwordService;
    private readonly IpBanService _ipBanService;
    private readonly PromptCatalog _prompts;
    private readonly RulesContent _rules;
    private const int MaxCharactersPerAccount = 10;
    private const int MaxPasswordAttempts = 3;

    public AuthenticationHandler(
        IAccountRepository accountRepository,
        ICharacterRepository characterRepository,
        IPasswordService passwordService,
        IpBanService ipBanService,
        PromptCatalog prompts,
        RulesContent rules)
    {
        _rules = rules;
        _accountRepository = accountRepository;
        _characterRepository = characterRepository;
        _passwordService = passwordService;
        _ipBanService = ipBanService;
        _prompts = prompts;
    }

    /// <summary>
    /// Process user input based on current connection state
    /// Returns the next state and a message to send to the user
    /// </summary>
    public async Task<(ConnectionState nextState, string message, SessionData updatedSession)> ProcessInputAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken = default)
    {
        return session.State switch
        {
            ConnectionState.GetAccountName => await HandleGetAccountNameAsync(input, session, cancellationToken),
            ConnectionState.ConfirmAccountName => await HandleConfirmAccountNameAsync(input, session, cancellationToken),
            ConnectionState.GetPassword => await HandleGetPasswordAsync(input, session, cancellationToken),
            ConnectionState.GetNewPassword => await HandleGetNewPasswordAsync(input, session, cancellationToken),
            ConnectionState.ConfirmNewPassword => await HandleConfirmNewPasswordAsync(input, session, cancellationToken),
            ConnectionState.CharacterSelection => await HandleCharacterSelectionAsync(input, session, cancellationToken),
            ConnectionState.GetCharacterName => await HandleGetCharacterNameAsync(input, session, cancellationToken),
            ConnectionState.ConfirmCharacterName => await HandleConfirmCharacterNameAsync(input, session, cancellationToken),
            ConnectionState.SelectSex => await HandleSelectSexAsync(input, session, cancellationToken),
            ConnectionState.SelectRace => await HandleSelectRaceAsync(input, session, cancellationToken),
            ConnectionState.SelectClass => await HandleSelectClassAsync(input, session, cancellationToken),
            ConnectionState.ShowMotd => await HandleShowMotdAsync(input, session, cancellationToken),
            ConnectionState.MainMenu => await HandleMainMenuAsync(input, session, cancellationToken),
            ConnectionState.ConfirmDelete => await HandleConfirmDeleteAsync(input, session, cancellationToken),
            _ => throw new InvalidOperationException($"Unhandled connection state: {session.State}")
        };
    }

    private async Task<(ConnectionState, string, SessionData)> HandleGetAccountNameAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var username = input.Trim();
        
        // Validate username (3-16 characters, letters only)
        if (username.Length < 3 || username.Length > 16 || !username.All(char.IsLetter))
        {
            return (ConnectionState.GetAccountName, 
                "Account name must be 3-16 letters only.\n" + _prompts.GetAccountNamePrompt(), 
                session);
        }

        session.AccountUsername = username;
        
        // Check if account exists
        var account = await _accountRepository.GetByUsernameAsync(username, cancellationToken);
        
        if (account != null)
        {
            // Existing account - ask for password
            session.AccountId = account.AccountId;
            return (ConnectionState.GetPassword, _prompts.GetPasswordPrompt(), session);
        }
        else
        {
            // New account - confirm name
            return (ConnectionState.ConfirmAccountName, 
                _prompts.GetConfirmAccountNamePrompt(username), 
                session);
        }
    }

    private Task<(ConnectionState, string, SessionData)> HandleConfirmAccountNameAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var answer = input.Trim().ToLower();
        
        if (answer == "y" || answer == "yes")
        {
            return Task.FromResult((ConnectionState.GetNewPassword, 
                _prompts.GetNewPasswordPrompt(), 
                session));
        }
        else
        {
            // Start over
            session.AccountUsername = null;
            return Task.FromResult((ConnectionState.GetAccountName, 
                _prompts.GetAccountNamePrompt(), 
                session));
        }
    }

    private async Task<(ConnectionState, string, SessionData)> HandleGetPasswordAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var password = input.Trim();
        
        if (session.AccountId == null)
        {
            throw new InvalidOperationException("AccountId is null in GetPassword state");
        }

        var account = await _accountRepository.GetByIdAsync(session.AccountId.Value, cancellationToken);
        
        if (account == null || !_passwordService.VerifyPassword(password, account.PasswordHash))
        {
            session.PasswordAttempts++;
            
            // Record failed attempt for this IP
            if (!string.IsNullOrEmpty(session.IpAddress))
            {
                var wasBanned = _ipBanService.RecordFailedAttempt(session.IpAddress);
                if (wasBanned)
                {
                    var banTime = _ipBanService.GetRemainingBanTime(session.IpAddress);
                    var banMessage = banTime.HasValue 
                        ? $"\nYour IP has been banned for {banTime.Value.TotalMinutes:F0} minutes due to too many failed attempts.\n"
                        : "\nYour IP has been banned due to too many failed attempts.\n";
                    
                    return (ConnectionState.Close,
                        banMessage,
                        session);
                }
            }
            
            // Check if max attempts reached for this session
            if (session.PasswordAttempts >= MaxPasswordAttempts)
            {
                return (ConnectionState.Close,
                    _prompts.GetTooManyFailedAttempts(),
                    session);
            }
            
            var remainingAttempts = MaxPasswordAttempts - session.PasswordAttempts;
            return (ConnectionState.GetPassword, 
                _prompts.GetInvalidPasswordWithAttempts(remainingAttempts) + _prompts.GetPasswordPrompt(), 
                session);
        }

        // Password correct - clear failed attempts and continue
        session.PasswordAttempts = 0;
        if (!string.IsNullOrEmpty(session.IpAddress))
        {
            _ipBanService.ClearFailedAttempts(session.IpAddress);
        }
        
        // Update last login and load characters
        await _accountRepository.UpdateLastLoginAsync(account.AccountId, cancellationToken);
        
        var characters = await _characterRepository.GetByAccountIdAsync(account.AccountId, cancellationToken);
        session.Characters = characters.Select(c => new CharacterListItem(
            c.CharacterId, 
            c.Name, 
            c.Level, 
            c.CharacterClass,
            c.Race)).ToList();

        return (ConnectionState.CharacterSelection, 
            _prompts.GetCharacterSelectionMenu(session.Characters), 
            session);
    }

    private Task<(ConnectionState, string, SessionData)> HandleGetNewPasswordAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var password = input.Trim();
        
        // Validate password (minimum 6 characters)
        if (password.Length < 6)
        {
            return Task.FromResult((ConnectionState.GetNewPassword, 
                "Password must be at least 6 characters.\n" + _prompts.GetNewPasswordPrompt(), 
                session));
        }

        session.PendingPassword = password;
        return Task.FromResult((ConnectionState.ConfirmNewPassword, 
            _prompts.GetConfirmPasswordPrompt(), 
            session));
    }

    private async Task<(ConnectionState, string, SessionData)> HandleConfirmNewPasswordAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var password = input.Trim();
        
        if (password != session.PendingPassword)
        {
            session.PendingPassword = null;
            return (ConnectionState.GetNewPassword, 
                _prompts.GetPasswordMismatch() + _prompts.GetNewPasswordPrompt(), 
                session);
        }

        // Create the account
        var passwordHash = _passwordService.HashPassword(password);
        var account = new Account
        {
            Username = session.AccountUsername!,
            PasswordHash = passwordHash
        };

        account = await _accountRepository.CreateAsync(account, cancellationToken);
        session.AccountId = account.AccountId;
        session.PendingPassword = null;
        session.Characters = new List<CharacterListItem>();

        return (ConnectionState.CharacterSelection, 
            _prompts.GetAccountCreated() + _prompts.GetCharacterSelectionMenu(session.Characters), 
            session);
    }

    private async Task<(ConnectionState, string, SessionData)> HandleCharacterSelectionAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var choice = input.Trim().ToLower();

        // Create new character
        if (choice == "n" || choice == "new")
        {
            // Check character limit
            var charCount = await _characterRepository.GetCharacterCountByAccountIdAsync(
                session.AccountId!.Value, cancellationToken);
            
            if (charCount >= MaxCharactersPerAccount)
            {
                return (ConnectionState.CharacterSelection,
                    _prompts.GetCharacterLimitReached(MaxCharactersPerAccount) + 
                    _prompts.GetCharacterSelectionMenu(session.Characters!),
                    session);
            }

            return (ConnectionState.GetCharacterName, _prompts.GetCharacterNamePrompt(), session);
        }

        // Delete character
        if (choice == "d" || choice == "delete")
        {
            return (ConnectionState.ConfirmDelete, "Enter the number of the character to delete: ", session);
        }

        // Quit
        if (choice == "q" || choice == "quit")
        {
            return (ConnectionState.Close, _prompts.GetGoodbyeMessage(), session);
        }

        // Select character by number
        if (int.TryParse(choice, out int charIndex) && charIndex > 0 && charIndex <= session.Characters!.Count)
        {
            var selectedChar = session.Characters[charIndex - 1];

            // Removing a class or race that characters use is refused (#35), so this only happens if the content
            // was edited by hand. Stop here rather than play a character the rules don't know.
            var unknown = UnknownRulesIds(selectedChar);
            if (unknown is not null)
            {
                Console.WriteLine($"Character {selectedChar.CharacterId} ({selectedChar.Name}) can't be played: {unknown}");
                return (ConnectionState.CharacterSelection,
                    $"{selectedChar.Name} can't be played right now ({unknown}). Please contact an immortal.\n" +
                    _prompts.GetCharacterSelectionMenu(session.Characters),
                    session);
            }

            session.SelectedCharacterId = selectedChar.CharacterId;
            
            return (ConnectionState.ShowMotd, _prompts.GetMotd(), session);
        }

        return (ConnectionState.CharacterSelection, 
            _prompts.GetInvalidOption() + _prompts.GetCharacterSelectionMenu(session.Characters!), 
            session);
    }

    private string? UnknownRulesIds(CharacterListItem character)
    {
        var problems = new List<string>();
        if (!_rules.Classes.ContainsKey(character.CharacterClass))
        {
            problems.Add($"unknown class '{character.CharacterClass}'");
        }

        if (!_rules.Races.ContainsKey(character.Race))
        {
            problems.Add($"unknown race '{character.Race}'");
        }

        return problems.Count == 0 ? null : string.Join(", ", problems);
    }

    private async Task<(ConnectionState, string, SessionData)> HandleGetCharacterNameAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var name = input.Trim();
        
        // Validate character name (3-16 letters only)
        if (name.Length < 3 || name.Length > 16 || !name.All(char.IsLetter))
        {
            return (ConnectionState.GetCharacterName, 
                "Character name must be 3-16 letters only.\n" + _prompts.GetCharacterNamePrompt(), 
                session);
        }

        // Check if character name is already taken
        var existing = await _characterRepository.GetByNameAsync(name, cancellationToken);
        if (existing != null)
        {
            return (ConnectionState.GetCharacterName, 
                "That name is already taken.\n" + _prompts.GetCharacterNamePrompt(), 
                session);
        }

        session.PendingCharacterName = name;
        return (ConnectionState.ConfirmCharacterName, 
            _prompts.GetConfirmCharacterNamePrompt(name), 
            session);
    }

    private Task<(ConnectionState, string, SessionData)> HandleConfirmCharacterNameAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var answer = input.Trim().ToLower();
        
        if (answer == "y" || answer == "yes")
        {
            return Task.FromResult((ConnectionState.SelectSex, _prompts.GetSexPrompt(), session));
        }
        else
        {
            session.PendingCharacterName = null;
            return Task.FromResult((ConnectionState.GetCharacterName, 
                _prompts.GetCharacterNamePrompt(), 
                session));
        }
    }

    private Task<(ConnectionState, string, SessionData)> HandleSelectSexAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var choice = input.Trim().ToLower();
        
        if (choice == "m" || choice == "male")
        {
            session.PendingSex = Sex.Male;
            return Task.FromResult((ConnectionState.SelectRace, _prompts.GetRacePrompt(), session));
        }
        else if (choice == "f" || choice == "female")
        {
            session.PendingSex = Sex.Female;
            return Task.FromResult((ConnectionState.SelectRace, _prompts.GetRacePrompt(), session));
        }

        return Task.FromResult((ConnectionState.SelectSex, 
            "Please choose M or F.\n" + _prompts.GetSexPrompt(), 
            session));
    }

    private Task<(ConnectionState, string, SessionData)> HandleSelectRaceAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var choice = input.Trim().ToLower();

        var race = _rules.CreationRaces.FirstOrDefault(r => r.MenuKey == choice);
        if (race is null)
        {
            return Task.FromResult((ConnectionState.SelectRace, 
                _prompts.GetInvalidOption() + _prompts.GetRacePrompt(), 
                session));
        }

        session.PendingRace = race.Id;
        return Task.FromResult((ConnectionState.SelectClass, _prompts.GetClassPrompt(race), session));
    }

    private async Task<(ConnectionState, string, SessionData)> HandleSelectClassAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var choice = input.Trim().ToLower();

        // Go back to race selection
        if (choice == "-")
        {
            session.PendingRace = null;
            return (ConnectionState.SelectRace, _prompts.GetRacePrompt(), session);
        }

        // Only the race's allowed classes can be picked
        var race = _rules.Races[session.PendingRace!];
        var characterClass = race.AllowedClasses.FirstOrDefault(c => c.MenuKey == choice);
        if (characterClass is null)
        {
            return (ConnectionState.SelectClass,
                _prompts.GetInvalidOption() + _prompts.GetClassPrompt(race),
                session);
        }

        session.PendingClass = characterClass.Id;

        // Create the character
        var character = new Character
        {
            AccountId = session.AccountId!.Value,
            Name = session.PendingCharacterName!,
            Sex = session.PendingSex.ToString(),
            Race = session.PendingRace!,
            CharacterClass = session.PendingClass,
            Level = 1,
            Experience = 0,
            
            // Base stats (all start at 11 like legacy)
            Strength = 11,
            Intelligence = 11,
            Wisdom = 11,
            Dexterity = 11,
            Constitution = 11,
            Charisma = 11,
            
            // Initial vitals (will be calculated based on class/race later)
            HitPoints = 20,
            MaxHitPoints = 20,
            Mana = 100,
            MaxMana = 100,
            Movement = 100,
            MaxMovement = 100,
            
            // Combat stats
            ArmorClass = 100,
            Hitroll = 0,
            Damroll = 0,
            Alignment = 0,
            
            // Starting location (room 1 - will be configurable later)
            RoomId = 1,
            Gold = 0,
            BankGold = 0
        };

        character = await _characterRepository.CreateAsync(character, cancellationToken);
        session.SelectedCharacterId = character.CharacterId;

        // Clear pending character creation data
        session.PendingCharacterName = null;
        session.PendingSex = null;
        session.PendingRace = null;
        session.PendingClass = null;

        return (ConnectionState.ShowMotd, 
            _prompts.GetCharacterCreated() + _prompts.GetMotd(), 
            session);
    }

    private Task<(ConnectionState, string, SessionData)> HandleShowMotdAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        return Task.FromResult((ConnectionState.MainMenu, _prompts.GetMainMenu(), session));
    }

    private Task<(ConnectionState, string, SessionData)> HandleMainMenuAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        var choice = input.Trim();

        if (choice == "1")
        {
            // Enter the game
            return Task.FromResult((ConnectionState.Playing, string.Empty, session));
        }
        else if (choice == "0")
        {
            // Quit
            return Task.FromResult((ConnectionState.Close, _prompts.GetGoodbyeMessage(), session));
        }

        return Task.FromResult((ConnectionState.MainMenu, 
            _prompts.GetInvalidOption() + _prompts.GetMainMenu(), 
            session));
    }

    private async Task<(ConnectionState, string, SessionData)> HandleConfirmDeleteAsync(
        string input,
        SessionData session,
        CancellationToken cancellationToken)
    {
        if (int.TryParse(input.Trim(), out int charIndex) && 
            charIndex > 0 && 
            charIndex <= session.Characters!.Count)
        {
            var charToDelete = session.Characters[charIndex - 1];
            await _characterRepository.DeleteAsync(charToDelete.CharacterId, cancellationToken);
            
            // Reload character list
            var characters = await _characterRepository.GetByAccountIdAsync(
                session.AccountId!.Value, cancellationToken);
            session.Characters = characters.Select(c => new CharacterListItem(
                c.CharacterId, 
                c.Name, 
                c.Level, 
                c.CharacterClass,
                c.Race)).ToList();

            return (ConnectionState.CharacterSelection, 
                $"Character '{charToDelete.Name}' deleted.\n" + 
                _prompts.GetCharacterSelectionMenu(session.Characters), 
                session);
        }

        return (ConnectionState.CharacterSelection, 
            _prompts.GetInvalidOption() + _prompts.GetCharacterSelectionMenu(session.Characters!), 
            session);
    }
}
