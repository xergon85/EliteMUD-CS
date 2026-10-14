# Database Migrations

When creating EF Core migrations that change storage format or schema:

**CRITICAL: Never drop tables/columns with data without migration**

**Correct migration pattern (preserves data):**
1. Add new column/table FIRST
2. Write SQL migration script to convert old format → new format
3. Verify migration on test data
4. THEN drop old column/table after data is migrated
5. Test rollback (`Down()` migration)

**Example - Safe data format migration:**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Step 1: Add new column
    migrationBuilder.AddColumn<string>(
        name: "NewFormatColumn",
        table: "TableName",
        type: "TEXT",
        nullable: true);

    // Step 2: Migrate existing data
    migrationBuilder.Sql(@"
        UPDATE TableName 
        SET NewFormatColumn = (/* SQL to convert old → new format */)
        WHERE OldFormatColumn IS NOT NULL;
    ");

    // Step 3: Drop old column/table (only after migration)
    migrationBuilder.DropColumn(name: "OldFormatColumn", table: "TableName");
}
```

**Early development exception:**
- During early development, a direct schema change that loses data is acceptable only when no database it will run
  against holds data worth keeping. The server applies pending migrations at startup (`ServerBootstrap`), so a local
  development database with characters in it counts.
- Document in commit message when data loss occurs
- Example: Commit 77cc072 dropped `CharacterInventory` without migration; use this pattern only with a disposable
  database.

**Production rule:**
- Always preserve existing data through migration scripts
- Test migration on copy of production database first
- Keep backup before applying destructive migrations
