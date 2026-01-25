using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteMud.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContainerPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContainerId",
                table: "CharacterInventory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjectState",
                table: "CharacterInventory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SequenceOrder",
                table: "CharacterInventory",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterInventory_ContainerId",
                table: "CharacterInventory",
                column: "ContainerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterInventory_CharacterInventory_ContainerId",
                table: "CharacterInventory",
                column: "ContainerId",
                principalTable: "CharacterInventory",
                principalColumn: "InventoryId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterInventory_CharacterInventory_ContainerId",
                table: "CharacterInventory");

            migrationBuilder.DropIndex(
                name: "IX_CharacterInventory_ContainerId",
                table: "CharacterInventory");

            migrationBuilder.DropColumn(
                name: "ContainerId",
                table: "CharacterInventory");

            migrationBuilder.DropColumn(
                name: "ObjectState",
                table: "CharacterInventory");

            migrationBuilder.DropColumn(
                name: "SequenceOrder",
                table: "CharacterInventory");
        }
    }
}
