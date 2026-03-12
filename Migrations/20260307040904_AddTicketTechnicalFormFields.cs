using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketTechnicalFormFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AttachmentPath",
                table: "Tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssignedTechnician",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AfterEvidencePath",
                table: "Tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BeforeEvidencePath",
                table: "Tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitialConditionNotes",
                table: "Tickets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreventiveRecommendations",
                table: "Tickets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepairActionsPerformed",
                table: "Tickets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootCause",
                table: "Tickets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SparePartDetails",
                table: "Tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SparePartPurchased",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SparePartRequired",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalTestsPerformed",
                table: "Tickets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UserConformityConfirmed",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AfterEvidencePath",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "BeforeEvidencePath",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "InitialConditionNotes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "PreventiveRecommendations",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RepairActionsPerformed",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RootCause",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SparePartDetails",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SparePartPurchased",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SparePartRequired",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TechnicalTestsPerformed",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "UserConformityConfirmed",
                table: "Tickets");

            migrationBuilder.AlterColumn<string>(
                name: "AttachmentPath",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssignedTechnician",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
