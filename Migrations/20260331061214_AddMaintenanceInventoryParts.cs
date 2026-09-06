using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceInventoryParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceInventoryParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PartName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Subcategory = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SystemType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EquipmentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CompatibleModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tenencia = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QuantityOnHand = table.Column<int>(type: "int", nullable: false),
                    MinimumStock = table.Column<int>(type: "int", nullable: false),
                    StockStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Supplier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnitCostCordoba = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitCostUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ShelfLocation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceInventoryParts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInventoryParts_PartCode",
                table: "MaintenanceInventoryParts",
                column: "PartCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceInventoryParts");
        }
    }
}
