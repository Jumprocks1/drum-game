using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrumGame.Game.Migrations
{
    public partial class Mods : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Extra",
                table: "Replays",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mods",
                table: "Replays",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Extra",
                table: "Replays");

            migrationBuilder.DropColumn(
                name: "Mods",
                table: "Replays");
        }
    }
}
