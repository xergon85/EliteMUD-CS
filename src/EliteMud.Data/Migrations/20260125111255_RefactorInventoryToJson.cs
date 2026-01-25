using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteMud.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorInventoryToJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterInventory");

            migrationBuilder.AddColumn<string>(
                name: "InventoryJson",
                table: "Characters",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InventoryJson",
                table: "Characters");

            migrationBuilder.CreateTable(
                name: "CharacterInventory",
                columns: table => new
                {
                    InventoryId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerId = table.Column<int>(type: "INTEGER", nullable: true),
                    ObjectDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ObjectState = table.Column<string>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    SequenceOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterInventory", x => x.InventoryId);
                    table.ForeignKey(
                        name: "FK_CharacterInventory_CharacterInventory_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "CharacterInventory",
                        principalColumn: "InventoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CharacterInventory_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterInventory_CharacterId",
                table: "CharacterInventory",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterInventory_ContainerId",
                table: "CharacterInventory",
                column: "ContainerId");
        }
    }
}
