using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddMaintenanceInventoryMasterFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraDescription",
                table: "MaintenanceInventoryParts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "MaintenanceInventoryParts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseUnitOfMeasure",
                table: "MaintenanceInventoryParts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraDescription",
                table: "MaintenanceInventoryParts");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "MaintenanceInventoryParts");

            migrationBuilder.DropColumn(
                name: "PurchaseUnitOfMeasure",
                table: "MaintenanceInventoryParts");
        }
    }
}
