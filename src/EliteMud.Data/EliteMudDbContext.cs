using Microsoft.EntityFrameworkCore;
using EliteMud.Data.Entities;

namespace EliteMud.Data;

public class EliteMudDbContext : DbContext
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<CharacterInventoryItem> CharacterInventory => Set<CharacterInventoryItem>();
    public DbSet<CharacterEquipmentItem> CharacterEquipment => Set<CharacterEquipmentItem>();

    public EliteMudDbContext(DbContextOptions<EliteMudDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Account configuration
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(16).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // One-to-many relationship with Characters
            entity.HasMany(e => e.Characters)
                .WithOne(e => e.Account)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Character configuration
        modelBuilder.Entity<Character>(entity =>
        {
            entity.HasKey(e => e.CharacterId);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.AccountId);
            entity.Property(e => e.Name).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Race).IsRequired();
            entity.Property(e => e.CharacterClass).IsRequired();
            entity.Property(e => e.Level).HasDefaultValue(1);
            entity.Property(e => e.Experience).HasDefaultValue(0);
            entity.Property(e => e.Gold).HasDefaultValue(0);
            entity.Property(e => e.BankGold).HasDefaultValue(0);
            entity.Property(e => e.Alignment).HasDefaultValue(0);
            entity.Property(e => e.PlayTimeMinutes).HasDefaultValue(0);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).IsRequired();

            // One-to-many relationship with Inventory
            entity.HasMany(e => e.Inventory)
                .WithOne(e => e.Character)
                .HasForeignKey(e => e.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-many relationship with Equipment
            entity.HasMany(e => e.Equipment)
                .WithOne(e => e.Character)
                .HasForeignKey(e => e.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CharacterInventoryItem configuration
        modelBuilder.Entity<CharacterInventoryItem>(entity =>
        {
            entity.HasKey(e => e.InventoryId);
            entity.HasIndex(e => e.CharacterId);
            entity.HasIndex(e => e.ContainerId);
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.SequenceOrder).HasDefaultValue(0);
            
            // Self-referencing relationship for container hierarchy
            entity.HasOne(e => e.Container)
                .WithMany(e => e.Contents)
                .HasForeignKey(e => e.ContainerId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete of contents
        });

        // CharacterEquipmentItem configuration
        modelBuilder.Entity<CharacterEquipmentItem>(entity =>
        {
            entity.HasKey(e => e.EquipmentId);
            entity.HasIndex(e => new { e.CharacterId, e.Slot }).IsUnique();
            entity.Property(e => e.Slot).IsRequired();
        });
    }
}
