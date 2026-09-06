using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceManagementSuite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalNotes",
                table: "FinanceReceipts",
                type: "nvarchar(700)",
                maxLength: 700,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "FinanceReceipts",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Pendiente");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "FinanceReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "FinanceReceipts",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CounterpartyId",
                table: "FinanceReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterpartyTaxId",
                table: "FinanceReceipts",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNotes",
                table: "FinanceInvoices",
                type: "nvarchar(700)",
                maxLength: 700,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "FinanceInvoices",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Pendiente");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "FinanceInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "FinanceInvoices",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CounterpartyId",
                table: "FinanceInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterpartyTaxId",
                table: "FinanceInvoices",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinanceAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCounterparties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceCounterparties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsdToCordobaRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DefaultEscalationHours = table.Column<int>(type: "int", nullable: false),
                    DefaultAlertThresholdPercent = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "FinanceSettings",
                columns: new[] { "UsdToCordobaRate", "DefaultEscalationHours", "DefaultAlertThresholdPercent", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { 36.5m, 24, 80, DateTime.UtcNow, "Migracion" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_ApprovalStatus_ReceiptDateUtc",
                table: "FinanceReceipts",
                columns: new[] { "ApprovalStatus", "ReceiptDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_CounterpartyId",
                table: "FinanceReceipts",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_ApprovalStatus_InvoiceDateUtc",
                table: "FinanceInvoices",
                columns: new[] { "ApprovalStatus", "InvoiceDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_CounterpartyId",
                table: "FinanceInvoices",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAttachments_DocumentType_DocumentId",
                table: "FinanceAttachments",
                columns: new[] { "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAuditLogs_EntityName_EntityId_PerformedAtUtc",
                table: "FinanceAuditLogs",
                columns: new[] { "EntityName", "EntityId", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCounterparties_Name_Type",
                table: "FinanceCounterparties",
                columns: new[] { "Name", "Type" });

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceInvoices_FinanceCounterparties_CounterpartyId",
                table: "FinanceInvoices",
                column: "CounterpartyId",
                principalTable: "FinanceCounterparties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceReceipts_FinanceCounterparties_CounterpartyId",
                table: "FinanceReceipts",
                column: "CounterpartyId",
                principalTable: "FinanceCounterparties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinanceInvoices_FinanceCounterparties_CounterpartyId",
                table: "FinanceInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_FinanceReceipts_FinanceCounterparties_CounterpartyId",
                table: "FinanceReceipts");

            migrationBuilder.DropTable(
                name: "FinanceAttachments");

            migrationBuilder.DropTable(
                name: "FinanceAuditLogs");

            migrationBuilder.DropTable(
                name: "FinanceCounterparties");

            migrationBuilder.DropTable(
                name: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_FinanceReceipts_ApprovalStatus_ReceiptDateUtc",
                table: "FinanceReceipts");

            migrationBuilder.DropIndex(
                name: "IX_FinanceReceipts_CounterpartyId",
                table: "FinanceReceipts");

            migrationBuilder.DropIndex(
                name: "IX_FinanceInvoices_ApprovalStatus_InvoiceDateUtc",
                table: "FinanceInvoices");

            migrationBuilder.DropIndex(
                name: "IX_FinanceInvoices_CounterpartyId",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "ApprovalNotes",
                table: "FinanceReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "FinanceReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "FinanceReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "FinanceReceipts");

            migrationBuilder.DropColumn(
                name: "CounterpartyId",
                table: "FinanceReceipts");

            migrationBuilder.DropColumn(
                name: "CounterpartyTaxId",
                table: "FinanceReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovalNotes",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "CounterpartyId",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "CounterpartyTaxId",
                table: "FinanceInvoices");
        }
    }
}
