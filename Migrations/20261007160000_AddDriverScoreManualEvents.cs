using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261007160000_AddDriverScoreManualEvents")]
    public partial class AddDriverScoreManualEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriverScoreManualEvents",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    EventAt = table.Column<DateTime>(nullable: false),
                    Driver = table.Column<string>(maxLength: 160, nullable: false),
                    Vehicle = table.Column<string>(maxLength: 40, nullable: false),
                    EventType = table.Column<string>(maxLength: 120, nullable: false),
                    Group = table.Column<string>(maxLength: 120, nullable: true),
                    Location = table.Column<string>(maxLength: 80, nullable: true),
                    Observation = table.Column<string>(maxLength: 1000, nullable: true),
                    TimelyManaged = table.Column<bool>(nullable: false),
                    CoachingCompleted = table.Column<bool>(nullable: false),
                    RegisteredBy = table.Column<string>(maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_DriverScoreManualEvents", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_DriverScoreManualEvents_EventAt", table: "DriverScoreManualEvents", column: "EventAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "DriverScoreManualEvents");
    }
}
