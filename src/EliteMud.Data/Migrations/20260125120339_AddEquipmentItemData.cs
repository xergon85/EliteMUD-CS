using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteMud.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentItemData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemData",
                table: "CharacterEquipment",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ItemData",
                table: "CharacterEquipment");
        }
    }
}
