using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrumGame.Game.Migrations
{
    public partial class init1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Replays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MapId = table.Column<string>(type: "TEXT", nullable: true),
                    CompleteTimeTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    AccuracyHit = table.Column<long>(type: "INTEGER", nullable: false),
                    AccuracyTotal = table.Column<long>(type: "INTEGER", nullable: false),
                    Score = table.Column<long>(type: "INTEGER", nullable: false),
                    MaxCombo = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Replays", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Replays");
        }
    }
}
