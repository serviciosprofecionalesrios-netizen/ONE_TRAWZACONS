using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryItemCodeAndDispatchPartNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemCode",
                table: "TicketSparePartDispatches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartNumber",
                table: "TicketSparePartDispatches",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemCode",
                table: "MaintenanceInventoryParts",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ItemCode",
                table: "TicketSparePartDispatches");

            migrationBuilder.DropColumn(
                name: "PartNumber",
                table: "TicketSparePartDispatches");

            migrationBuilder.DropColumn(
                name: "ItemCode",
                table: "MaintenanceInventoryParts");
        }
    }
}
