using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceDocumentsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinanceInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CounterpartyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Concept = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    InvoiceDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReceivedFrom = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Concept = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ReceiptDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignatureToken = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SignedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignatureDeviceInfo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SignatureDataUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_InvoiceNumber",
                table: "FinanceInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_Site_InvoiceDateUtc",
                table: "FinanceInvoices",
                columns: new[] { "Site", "InvoiceDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_ReceiptNumber",
                table: "FinanceReceipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_SignatureToken",
                table: "FinanceReceipts",
                column: "SignatureToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_Site_ReceiptDateUtc",
                table: "FinanceReceipts",
                columns: new[] { "Site", "ReceiptDateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceInvoices");

            migrationBuilder.DropTable(
                name: "FinanceReceipts");
        }
    }
}
