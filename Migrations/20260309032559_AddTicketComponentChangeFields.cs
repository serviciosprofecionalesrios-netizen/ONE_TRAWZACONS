using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketComponentChangeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ChangedComponentCost",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangedComponentName",
                table: "Tickets",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ComponentChanged",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangedComponentCost",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ChangedComponentName",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ComponentChanged",
                table: "Tickets");
        }
    }
}
