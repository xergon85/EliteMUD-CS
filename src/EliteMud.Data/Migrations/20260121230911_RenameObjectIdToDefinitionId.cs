using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteMud.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameObjectIdToDefinitionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ObjectId",
                table: "CharacterInventory",
                newName: "ObjectDefinitionId");

            migrationBuilder.RenameColumn(
                name: "ObjectId",
                table: "CharacterEquipment",
                newName: "ObjectDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ObjectDefinitionId",
                table: "CharacterInventory",
                newName: "ObjectId");

            migrationBuilder.RenameColumn(
                name: "ObjectDefinitionId",
                table: "CharacterEquipment",
                newName: "ObjectId");
        }
    }
}
