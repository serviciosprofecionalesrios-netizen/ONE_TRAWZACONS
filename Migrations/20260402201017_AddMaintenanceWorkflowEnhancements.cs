using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceWorkflowEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostApprovalNotes",
                table: "Tickets",
                type: "nvarchar(800)",
                maxLength: 800,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CostApproved",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CostApprovedAtUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostApprovedBy",
                table: "Tickets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalCostCordoba",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalCostUsd",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LaborCostCordoba",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LaborCostUsd",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceStage",
                table: "Tickets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresCostApproval",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxActiveOrders",
                table: "MaintenanceTechnicians",
                type: "int",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<string>(
                name: "Shift",
                table: "MaintenanceTechnicians",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Department_MaintenanceStage_Status",
                table: "Tickets",
                columns: new[] { "Department", "MaintenanceStage", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTechnicians_Category_Shift_IsAvailable_IsActive",
                table: "MaintenanceTechnicians",
                columns: new[] { "Category", "Shift", "IsAvailable", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_Department_MaintenanceStage_Status",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceTechnicians_Category_Shift_IsAvailable_IsActive",
                table: "MaintenanceTechnicians");

            migrationBuilder.DropColumn(
                name: "CostApprovalNotes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CostApproved",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CostApprovedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CostApprovedBy",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ExternalCostCordoba",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ExternalCostUsd",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "LaborCostCordoba",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "LaborCostUsd",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "MaintenanceStage",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RequiresCostApproval",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "MaxActiveOrders",
                table: "MaintenanceTechnicians");

            migrationBuilder.DropColumn(
                name: "Shift",
                table: "MaintenanceTechnicians");
        }
    }
}
