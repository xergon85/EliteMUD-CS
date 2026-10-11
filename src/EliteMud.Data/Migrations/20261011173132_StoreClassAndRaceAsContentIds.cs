using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteMud.Data.Migrations
{
    /// <summary>
    /// Characters stored their race and class as C# enum names ("HalfElf", "MagicUser"). They now store the content
    /// ids (content/races, content/classes). Data only: the columns stay the same. Values not in the lists are left
    /// as they are; such a character is refused at login with a logged error.
    /// </summary>
    public partial class StoreClassAndRaceAsContentIds : Migration
    {
        // Old enum name -> content id
        private static readonly (string Old, string Id)[] Races =
        {
            ("Human", "human"), ("Troll", "troll"), ("Halfling", "halfling"), ("Dwarf", "dwarf"),
            ("Gnome", "gnome"), ("Elf", "elf"), ("HalfElf", "half-elf"), ("Fairy", "fairy"),
            ("Minotaur", "minotaur"), ("Ratman", "ratman"), ("Drow", "drow"), ("Lizardman", "lizard"),
            ("Draconian", "draconian")
        };

        private static readonly (string Old, string Id)[] Classes =
        {
            ("MagicUser", "magic-user"), ("Cleric", "cleric"), ("Thief", "thief"), ("Warrior", "warrior"),
            ("Psionicist", "psionicist"), ("Monk", "monk"), ("Bard", "bard"), ("Knight", "knight"),
            ("Wizard", "wizard"), ("Druid", "druid"), ("Assassin", "assassin"), ("Ranger", "ranger"),
            ("Illusionist", "illusionist"), ("Paladin", "paladin"), ("Mariner", "mariner"),
            ("Cavalier", "cavalier"), ("Ninja", "ninja")
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Rename("Race", Races.Select(r => (r.Old, r.Id))));
            migrationBuilder.Sql(Rename("CharacterClass", Classes.Select(c => (c.Old, c.Id))));
        }

        /// <summary>
        /// Turns every content id back into its enum name, not only the rows Up() changed: characters created after
        /// Up() store ids too, and the old code expects enum names. Ids can't predate Up(), since the only writer
        /// before it was the enum's ToString().
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Rename("Race", Races.Select(r => (r.Id, r.Old))));
            migrationBuilder.Sql(Rename("CharacterClass", Classes.Select(c => (c.Id, c.Old))));
        }

        private static string Rename(string column, IEnumerable<(string From, string To)> pairs)
        {
            var list = pairs.ToList();
            var cases = string.Join(" ", list.Select(p => $"WHEN '{p.From}' THEN '{p.To}'"));
            var from = string.Join(", ", list.Select(p => $"'{p.From}'"));
            return $"UPDATE Characters SET {column} = CASE {column} {cases} END WHERE {column} IN ({from});";
        }
    }
}
