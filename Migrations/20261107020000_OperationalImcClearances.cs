using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261107020000_OperationalImcClearances")]
public class OperationalImcClearances : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        migration.CreateTable("OperationalImcClearances", columns: table => new
        {
            Month = table.Column<string>(maxLength: 7, nullable: false),
            UnitKey = table.Column<string>(maxLength: 160, nullable: false),
            UnitCode = table.Column<string>(maxLength: 160, nullable: false)
        }, constraints: table => table.PrimaryKey("PK_OperationalImcClearances", row => new { row.Month, row.UnitKey }));
    }

    protected override void Down(MigrationBuilder migration) => migration.DropTable("OperationalImcClearances");
}
