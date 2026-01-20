using EliteMud.Game;

namespace EliteMud.Application.Session;

/// <summary>
/// Connection states for the login and character creation flow
/// Based on legacy CON_* states, adapted for multi-character support
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Playing - player is in the game
    /// </summary>
    Playing = 0,
    
    /// <summary>
    /// Asking for account username
    /// </summary>
    GetAccountName = 1,
    
    /// <summary>
    /// Confirm account name for new account
    /// </summary>
    ConfirmAccountName = 2,
    
    /// <summary>
    /// Get password for existing account
    /// </summary>
    GetPassword = 3,
    
    /// <summary>
    /// Get password for new account
    /// </summary>
    GetNewPassword = 4,
    
    /// <summary>
    /// Confirm password for new account
    /// </summary>
    ConfirmNewPassword = 5,
    
    /// <summary>
    /// Character selection menu
    /// </summary>
    CharacterSelection = 6,
    
    /// <summary>
    /// Get character name for new character
    /// </summary>
    GetCharacterName = 7,
    
    /// <summary>
    /// Confirm character name
    /// </summary>
    ConfirmCharacterName = 8,
    
    /// <summary>
    /// Select character sex
    /// </summary>
    SelectSex = 9,
    
    /// <summary>
    /// Select character race
    /// </summary>
    SelectRace = 10,
    
    /// <summary>
    /// Select character class
    /// </summary>
    SelectClass = 11,
    
    /// <summary>
    /// Show MOTD (Message of the Day)
    /// </summary>
    ShowMotd = 12,
    
    /// <summary>
    /// Main menu (Enter game, Quit, etc.)
    /// </summary>
    MainMenu = 13,
    
    /// <summary>
    /// Character deletion confirmation
    /// </summary>
    ConfirmDelete = 14,
    
    /// <summary>
    /// Disconnecting
    /// </summary>
    Close = 15
}

/// <summary>
/// Session data that persists across the connection state machine
/// </summary>
public class SessionData
{
    // Account info
    public int? AccountId { get; set; }
    public string? AccountUsername { get; set; }
    
    // Character creation temporary data
    public string? PendingCharacterName { get; set; }
    public string? PendingPassword { get; set; }
    public Sex? PendingSex { get; set; }
    public Race? PendingRace { get; set; }
    public CharacterClass? PendingClass { get; set; }
    
    // Current selected character
    public int? SelectedCharacterId { get; set; }
    
    // Character list for selection menu
    public List<CharacterListItem>? Characters { get; set; }
    
    // Security tracking
    public int PasswordAttempts { get; set; } = 0;
    public string? IpAddress { get; set; }
    
    // State tracking
    public ConnectionState State { get; set; } = ConnectionState.GetAccountName;
}

/// <summary>
/// Simplified character info for the selection menu
/// </summary>
public record CharacterListItem(int CharacterId, string Name, int Level, string CharacterClass);
