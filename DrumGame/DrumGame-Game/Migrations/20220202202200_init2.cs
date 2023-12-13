using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrumGame.Game.Migrations
{
    public partial class init2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Combo",
                table: "Replays",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Combo",
                table: "Replays");
        }
    }
}
