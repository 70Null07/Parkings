using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parkings.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddManualSpaceBlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMapFilters");

            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyBlocked",
                table: "ParkingSpaces",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManuallyBlocked",
                table: "ParkingSpaces");

            migrationBuilder.CreateTable(
                name: "UserMapFilters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PointsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMapFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMapFilters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMapFilters_UserId",
                table: "UserMapFilters",
                column: "UserId");
        }
    }
}
