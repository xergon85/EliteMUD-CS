using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteMud.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAffectsPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Affects",
                table: "Characters",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Affects",
                table: "Characters");
        }
    }
}
