using System;
using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260330235500_AddMaintenanceAppointments")]
    public partial class AddMaintenanceAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceAppointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestingUser = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tenencia = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaintenanceType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AssetOrArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AssignedTechnician = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RecipientUsers = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceAppointments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAppointments_AppointmentNumber",
                table: "MaintenanceAppointments",
                column: "AppointmentNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceAppointments");
        }
    }
}
