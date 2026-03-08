using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parkings.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddOccupancyLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckedAt",
                table: "Parkings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OccupancyLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingId = table.Column<int>(type: "int", nullable: false),
                    FreeSpots = table.Column<int>(type: "int", nullable: false),
                    BusySpots = table.Column<int>(type: "int", nullable: false),
                    TotalSpots = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OccupancyLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OccupancyLogs_Parkings_ParkingId",
                        column: x => x.ParkingId,
                        principalTable: "Parkings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OccupancyLogs_ParkingId",
                table: "OccupancyLogs",
                column: "ParkingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OccupancyLogs");

            migrationBuilder.DropColumn(
                name: "LastCheckedAt",
                table: "Parkings");
        }
    }
}
