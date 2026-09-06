using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceWorkflowEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FinanceAccountingEntryNumber",
                table: "Tickets",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanceErpExportReference",
                table: "Tickets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinanceErpExportedAtUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinanceEscalatedAtUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanceEscalatedTo",
                table: "Tickets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanceFinalPaymentMethod",
                table: "Tickets",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanceInvoiceNumber",
                table: "Tickets",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinancePaymentDateUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinancePurchaseOrderNumber",
                table: "Tickets",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanceReconciliationStatus",
                table: "Tickets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanceReturnReason",
                table: "Tickets",
                type: "nvarchar(700)",
                maxLength: 700,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinanceReturnedAtUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanceReturnedBy",
                table: "Tickets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FinanceReturnedForCorrection",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FinanceApprovalRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostCenter = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    MinAmountCordoba = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxAmountCordoba = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RequiredApprovals = table.Column<int>(type: "int", nullable: false),
                    EscalationHours = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceApprovalRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceMonthlyBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CostCenter = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    BudgetLimitCordoba = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HardStopOnOverrun = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceMonthlyBudgets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceApprovalRules_IsActive_RequestType_CostCenter_MinAmountCordoba_MaxAmountCordoba",
                table: "FinanceApprovalRules",
                columns: new[] { "IsActive", "RequestType", "CostCenter", "MinAmountCordoba", "MaxAmountCordoba" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceMonthlyBudgets_CostCenter_Year_Month",
                table: "FinanceMonthlyBudgets",
                columns: new[] { "CostCenter", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceApprovalRules");

            migrationBuilder.DropTable(
                name: "FinanceMonthlyBudgets");

            migrationBuilder.DropColumn(
                name: "FinanceAccountingEntryNumber",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceErpExportReference",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceErpExportedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceEscalatedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceEscalatedTo",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceFinalPaymentMethod",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceInvoiceNumber",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinancePaymentDateUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinancePurchaseOrderNumber",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceReconciliationStatus",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceReturnReason",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceReturnedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceReturnedBy",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FinanceReturnedForCorrection",
                table: "Tickets");
        }
    }
}
