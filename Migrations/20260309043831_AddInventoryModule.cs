using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssetTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Subcategory = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TechnicalSpecifications = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssignedTo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AssignedToEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WarrantyEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Supplier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CostCordoba = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CostUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MacAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OperatingSystem = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    OfficeVersion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Antivirus = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LastMaintenanceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextMaintenanceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_AssetCode",
                table: "InventoryItems",
                column: "AssetCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryItems");
        }
    }
}
