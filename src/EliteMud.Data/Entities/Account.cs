namespace EliteMud.Data.Entities;

public class Account
{
    public int AccountId { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<Character> Characters { get; set; } = new List<Character>();
}
