using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261020020000_DriverMasterManagement")]
public class DriverMasterManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("IsActive", "OperationalMasterRecords", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<string>("SourceKey", "OperationalMasterRecords", maxLength: 250, nullable: true);
        foreach (var column in new[] { "FirstName", "SecondName", "FirstSurname", "SecondSurname" })
            migrationBuilder.AddColumn<string>(column, "OperationalMasterRecords", maxLength: 40, nullable: true);
        migrationBuilder.Sql("UPDATE \"OperationalMasterRecords\" SET \"SourceKey\" = \"RecordKey\"");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "IsActive", "SourceKey", "FirstName", "SecondName", "FirstSurname", "SecondSurname" })
            migrationBuilder.DropColumn(column, "OperationalMasterRecords");
    }
}
