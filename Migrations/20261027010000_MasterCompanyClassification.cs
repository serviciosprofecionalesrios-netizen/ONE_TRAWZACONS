using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261027010000_MasterCompanyClassification")]
public class MasterCompanyClassification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing records belong to EQUINOX; ownership and identity remain unchanged.
        migrationBuilder.AddColumn<string>("CompanyClassification", "OperationalMasterRecords", maxLength: 30, nullable: false, defaultValue: "EQUINOX");
        migrationBuilder.AddColumn<string>("HolcimReference", "OperationalMasterRecords", maxLength: 40, nullable: true);
        migrationBuilder.AddColumn<string>("HolcimSourceJson", "OperationalMasterRecords", nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "CompanyClassification", "HolcimReference", "HolcimSourceJson" })
            migrationBuilder.DropColumn(column, "OperationalMasterRecords");
    }
}
