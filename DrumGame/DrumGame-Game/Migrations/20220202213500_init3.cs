using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrumGame.Game.Migrations
{
    public partial class init3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bad",
                table: "Replays",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Good",
                table: "Replays",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Miss",
                table: "Replays",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Perfect",
                table: "Replays",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartNote",
                table: "Replays",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "StartPosition",
                table: "Replays",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bad",
                table: "Replays");

            migrationBuilder.DropColumn(
                name: "Good",
                table: "Replays");

            migrationBuilder.DropColumn(
                name: "Miss",
                table: "Replays");

            migrationBuilder.DropColumn(
                name: "Perfect",
                table: "Replays");

            migrationBuilder.DropColumn(
                name: "StartNote",
                table: "Replays");

            migrationBuilder.DropColumn(
                name: "StartPosition",
                table: "Replays");
        }
    }
}
