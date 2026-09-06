using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketMaintenanceFailureTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DamagedElement",
                table: "Tickets",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExitOdometerKm",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCategory",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FailureOdometerKm",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitCode",
                table: "Tickets",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DamagedElement",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ExitOdometerKm",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FailureCategory",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FailureOdometerKm",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "UnitCode",
                table: "Tickets");
        }
    }
}
