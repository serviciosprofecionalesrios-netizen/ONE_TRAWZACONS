using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceTechnicianModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TechnicianCategory",
                table: "Tickets",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicianCategory",
                table: "MaintenanceAppointments",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceTechnicians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EmergencyRelationship = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmergencyContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTechnicians", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTechnicians_Category_IsAvailable_IsActive",
                table: "MaintenanceTechnicians",
                columns: new[] { "Category", "IsAvailable", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTechnicians_FullName",
                table: "MaintenanceTechnicians",
                column: "FullName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceTechnicians");

            migrationBuilder.DropColumn(
                name: "TechnicianCategory",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TechnicianCategory",
                table: "MaintenanceAppointments");
        }
    }
}
