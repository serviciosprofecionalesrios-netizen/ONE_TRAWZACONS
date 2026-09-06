using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddHsPermitApprovalSlaAndInspectionEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalResponseDueAtUtc",
                table: "HsWorkPermits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipmentCode",
                table: "HsInspections",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalResponseDueAtUtc",
                table: "HsWorkPermits");

            migrationBuilder.DropColumn(
                name: "EquipmentCode",
                table: "HsInspections");
        }
    }
}
