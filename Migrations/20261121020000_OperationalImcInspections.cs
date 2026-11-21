using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261121020000_OperationalImcInspections")]
public class OperationalImcInspections : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        migration.CreateTable("OperationalImcInspections", table => new
        {
            Month = table.Column<string>(maxLength: 7, nullable: false),
            UnitKey = table.Column<string>(maxLength: 160, nullable: false),
            UnitCode = table.Column<string>(maxLength: 160, nullable: false),
            PresentedAt = table.Column<DateTime>(
                type: ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL" ? "timestamp without time zone" : "datetime2",
                nullable: false),
            Color = table.Column<string>(maxLength: 10, nullable: false)
        }, constraints: table =>
            table.PrimaryKey("PK_OperationalImcInspections", row => new { row.Month, row.UnitKey, row.PresentedAt, row.Color }));
    }

    protected override void Down(MigrationBuilder migration) => migration.DropTable("OperationalImcInspections");
}
