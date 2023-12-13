using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrumGame.Game.Migrations
{
    public partial class MapRating : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Beatmaps",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Beatmaps");
        }
    }
}
