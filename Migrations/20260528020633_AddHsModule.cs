using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddHsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HsCorrectiveActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosureNotes = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsCorrectiveActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReportedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IncidentType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImmediateAction = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    LostTime = table.Column<bool>(type: "bit", nullable: false),
                    LostDays = table.Column<int>(type: "int", nullable: false),
                    EvidencePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsIncidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsInspections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InspectionDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Inspector = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ScorePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    FindingsCount = table.Column<int>(type: "int", nullable: false),
                    CriticalFindingsCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsTrainingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrainingDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Trainer = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AttendeeCount = table.Column<int>(type: "int", nullable: false),
                    RequiredTraining = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsTrainingRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsWorkPermits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermitNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PermitType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Controls = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsWorkPermits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HsCorrectiveActions_Status_Priority_DueDateUtc",
                table: "HsCorrectiveActions",
                columns: new[] { "Status", "Priority", "DueDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_IncidentNumber",
                table: "HsIncidents",
                column: "IncidentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_Status_Severity_OccurredAtUtc",
                table: "HsIncidents",
                columns: new[] { "Status", "Severity", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HsInspections_InspectionNumber",
                table: "HsInspections",
                column: "InspectionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsInspections_Site_Area_InspectionDateUtc",
                table: "HsInspections",
                columns: new[] { "Site", "Area", "InspectionDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HsTrainingRecords_Site_TrainingDateUtc",
                table: "HsTrainingRecords",
                columns: new[] { "Site", "TrainingDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HsWorkPermits_PermitNumber",
                table: "HsWorkPermits",
                column: "PermitNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsWorkPermits_Status_StartAtUtc",
                table: "HsWorkPermits",
                columns: new[] { "Status", "StartAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HsCorrectiveActions");

            migrationBuilder.DropTable(
                name: "HsIncidents");

            migrationBuilder.DropTable(
                name: "HsInspections");

            migrationBuilder.DropTable(
                name: "HsTrainingRecords");

            migrationBuilder.DropTable(
                name: "HsWorkPermits");
        }
    }
}
