using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketTechnicalDocsAndObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExitOrderPath",
                table: "Tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observations",
                table: "Tickets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalSheetPath",
                table: "Tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExitOrderPath",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Observations",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TechnicalSheetPath",
                table: "Tickets");
        }
    }
}
