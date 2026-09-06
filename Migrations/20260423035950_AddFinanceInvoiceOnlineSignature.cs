using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceInvoiceOnlineSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignatureDataUrl",
                table: "FinanceInvoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureDeviceInfo",
                table: "FinanceInvoices",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureToken",
                table: "FinanceInvoices",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAtUtc",
                table: "FinanceInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedByName",
                table: "FinanceInvoices",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE FinanceInvoices
SET SignatureToken = REPLACE(CONVERT(varchar(36), NEWID()), '-', '')
WHERE SignatureToken IS NULL;
");

            migrationBuilder.AlterColumn<string>(
                name: "SignatureToken",
                table: "FinanceInvoices",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_SignatureToken",
                table: "FinanceInvoices",
                column: "SignatureToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinanceInvoices_SignatureToken",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "SignatureDataUrl",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "SignatureDeviceInfo",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "SignatureToken",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "SignedAtUtc",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "SignedByName",
                table: "FinanceInvoices");
        }
    }
}
