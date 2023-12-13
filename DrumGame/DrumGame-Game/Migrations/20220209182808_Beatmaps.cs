using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrumGame.Game.Migrations
{
    public partial class Beatmaps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"drop view if exists Beatmaps;");
            migrationBuilder.Sql(@"
                create view Beatmaps as
                select r.MapId as Id, MAX(r.CompleteTimeTicks) as PlayTime
                from Replays r group by r.MapId;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"drop view Beatmaps;");
        }
    }
}
