using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EliteMud.Data;

public class EliteMudDbContextFactory : IDesignTimeDbContextFactory<EliteMudDbContext>
{
    public EliteMudDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EliteMudDbContext>();
        
        // Use a default connection string for migrations
        // The actual connection string will be configured at runtime
        optionsBuilder.UseSqlite("Data Source=elitemud.db");

        return new EliteMudDbContext(optionsBuilder.Options);
    }
}
