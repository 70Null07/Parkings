using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parkings.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddModelAndAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StreamLogin",
                table: "Parkings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreamPassword",
                table: "Parkings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YoloModel",
                table: "Parkings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StreamLogin",
                table: "Parkings");

            migrationBuilder.DropColumn(
                name: "StreamPassword",
                table: "Parkings");

            migrationBuilder.DropColumn(
                name: "YoloModel",
                table: "Parkings");
        }
    }
}
