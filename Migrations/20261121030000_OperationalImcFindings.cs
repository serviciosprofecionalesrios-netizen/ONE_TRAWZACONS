using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261121030000_OperationalImcFindings")]
public class OperationalImcFindings : Migration
{
    protected override void Up(MigrationBuilder migration) =>
        migration.AddColumn<string>("FindingsJson", "OperationalImcInspections", nullable: true);

    protected override void Down(MigrationBuilder migration) =>
        migration.DropColumn("FindingsJson", "OperationalImcInspections");
}
