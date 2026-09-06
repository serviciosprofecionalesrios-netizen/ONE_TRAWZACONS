using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260330221500_AddTicketTenencia")]
    public partial class AddTicketTenencia : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tenencia",
                table: "Tickets",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tenencia",
                table: "Tickets");
        }
    }
}
