using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceInventoryFlowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastIssueDate",
                table: "MaintenanceInventoryParts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityInTransit",
                table: "MaintenanceInventoryParts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuantityIssued",
                table: "MaintenanceInventoryParts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastIssueDate",
                table: "MaintenanceInventoryParts");

            migrationBuilder.DropColumn(
                name: "QuantityInTransit",
                table: "MaintenanceInventoryParts");

            migrationBuilder.DropColumn(
                name: "QuantityIssued",
                table: "MaintenanceInventoryParts");
        }
    }
}
