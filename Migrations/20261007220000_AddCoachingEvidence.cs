using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261007220000_AddCoachingEvidence")]
public class AddCoachingEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(name: "CoachingEvidence", table: "DriverScoreManualEvents", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CoachingEvidenceName", table: "DriverScoreManualEvents", maxLength: 255, nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CoachingEvidence", table: "DriverScoreManualEvents");
        migrationBuilder.DropColumn(name: "CoachingEvidenceName", table: "DriverScoreManualEvents");
    }
}
