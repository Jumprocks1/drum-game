using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrumGame.Game.Migrations
{
    public partial class ReplayInfotime : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PlaybackSpeed",
                table: "Replays",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<long>(
                name: "StartTimeTicks",
                table: "Replays",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaybackSpeed",
                table: "Replays");

            migrationBuilder.DropColumn(
                name: "StartTimeTicks",
                table: "Replays");
        }
    }
}
