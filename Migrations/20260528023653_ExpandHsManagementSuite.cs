using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class ExpandHsManagementSuite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChecklistNotes",
                table: "HsWorkPermits",
                type: "nvarchar(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAreaIsolation",
                table: "HsWorkPermits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasEmergencyPlan",
                table: "HsWorkPermits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasPpe",
                table: "HsWorkPermits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasRiskAssessment",
                table: "HsWorkPermits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSupervisorApproval",
                table: "HsWorkPermits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeCode",
                table: "HsTrainingRecords",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeName",
                table: "HsTrainingRecords",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidencePath",
                table: "HsTrainingRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDateUtc",
                table: "HsTrainingRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "HsTrainingRecords",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Result",
                table: "HsTrainingRecords",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurrentControls",
                table: "HsIncidents",
                type: "nvarchar(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiveWhyAnalysis",
                table: "HsIncidents",
                type: "nvarchar(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImmediateCause",
                table: "HsIncidents",
                type: "nvarchar(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvestigationClosedAtUtc",
                table: "HsIncidents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvestigationDueDateUtc",
                table: "HsIncidents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Investigator",
                table: "HsIncidents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendedControls",
                table: "HsIncidents",
                type: "nvarchar(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidualRiskLevel",
                table: "HsIncidents",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ResidualRiskProbability",
                table: "HsIncidents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ResidualRiskScore",
                table: "HsIncidents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ResidualRiskSeverity",
                table: "HsIncidents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                table: "HsIncidents",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RiskProbability",
                table: "HsIncidents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                table: "HsIncidents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RiskSeverity",
                table: "HsIncidents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Witnesses",
                table: "HsIncidents",
                type: "nvarchar(800)",
                maxLength: 800,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNotes",
                table: "HsCorrectiveActions",
                type: "nvarchar(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "HsCorrectiveActions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "HsCorrectiveActions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "HsCorrectiveActions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "HsCorrectiveActions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HsDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EffectiveDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HsDocuments_Code",
                table: "HsDocuments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsDocuments_DocumentType_Status_ReviewDateUtc",
                table: "HsDocuments",
                columns: new[] { "DocumentType", "Status", "ReviewDateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HsDocuments");

            migrationBuilder.DropColumn(
                name: "ChecklistNotes",
                table: "HsWorkPermits");

            migrationBuilder.DropColumn(
                name: "HasAreaIsolation",
                table: "HsWorkPermits");

            migrationBuilder.DropColumn(
                name: "HasEmergencyPlan",
                table: "HsWorkPermits");

            migrationBuilder.DropColumn(
                name: "HasPpe",
                table: "HsWorkPermits");

            migrationBuilder.DropColumn(
                name: "HasRiskAssessment",
                table: "HsWorkPermits");

            migrationBuilder.DropColumn(
                name: "HasSupervisorApproval",
                table: "HsWorkPermits");

            migrationBuilder.DropColumn(
                name: "EmployeeCode",
                table: "HsTrainingRecords");

            migrationBuilder.DropColumn(
                name: "EmployeeName",
                table: "HsTrainingRecords");

            migrationBuilder.DropColumn(
                name: "EvidencePath",
                table: "HsTrainingRecords");

            migrationBuilder.DropColumn(
                name: "ExpirationDateUtc",
                table: "HsTrainingRecords");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "HsTrainingRecords");

            migrationBuilder.DropColumn(
                name: "Result",
                table: "HsTrainingRecords");

            migrationBuilder.DropColumn(
                name: "CurrentControls",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "FiveWhyAnalysis",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "ImmediateCause",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "InvestigationClosedAtUtc",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "InvestigationDueDateUtc",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "Investigator",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "RecommendedControls",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "ResidualRiskLevel",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "ResidualRiskProbability",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "ResidualRiskScore",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "ResidualRiskSeverity",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "RiskProbability",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "RiskSeverity",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "Witnesses",
                table: "HsIncidents");

            migrationBuilder.DropColumn(
                name: "ApprovalNotes",
                table: "HsCorrectiveActions");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "HsCorrectiveActions");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "HsCorrectiveActions");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "HsCorrectiveActions");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "HsCorrectiveActions");
        }
    }
}
