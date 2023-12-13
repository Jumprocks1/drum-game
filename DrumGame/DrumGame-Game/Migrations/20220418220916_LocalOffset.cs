using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrumGame.Game.Migrations
{
    public partial class LocalOffset : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"drop view if exists Beatmaps;");
            migrationBuilder.CreateTable(
                name: "Beatmaps",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PlayTime = table.Column<long>(type: "INTEGER", nullable: false),
                    LocalOffset = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beatmaps", x => x.Id);
                });

            migrationBuilder.Sql(@"
                INSERT INTO Beatmaps 
                SELECT Replays.MapId, MAX(Replays.completetimeticks),0 from Replays GROUP BY Replays.mapid
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Beatmaps");
        }
    }
}
