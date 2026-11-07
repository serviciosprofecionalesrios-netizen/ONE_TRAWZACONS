using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261107030000_OperationalImcColors")]
public class OperationalImcColors : Migration
{
    protected override void Up(MigrationBuilder migration) =>
        migration.AddColumn<string>("Color", "OperationalImcClearances", maxLength: 10, nullable: false, defaultValue: "Verde");

    protected override void Down(MigrationBuilder migration) =>
        migration.DropColumn("Color", "OperationalImcClearances");
}
