using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intersect.Server.Migrations.Sqlite.Game
{
    public partial class AddAnimationFrameSpeed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnimationFrameSpeed",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 200);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnimationFrameSpeed",
                table: "Items");
        }
    }
}
