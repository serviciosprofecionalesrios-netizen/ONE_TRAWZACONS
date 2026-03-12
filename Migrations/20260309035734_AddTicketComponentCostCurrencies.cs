using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketComponentCostCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ChangedComponentCostCordoba",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChangedComponentCostUsd",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangedComponentCostCordoba",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ChangedComponentCostUsd",
                table: "Tickets");
        }
    }
}
