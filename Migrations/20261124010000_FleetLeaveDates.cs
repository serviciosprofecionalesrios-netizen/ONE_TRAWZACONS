using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261124010000_FleetLeaveDates")]
public class FleetLeaveDates : Migration
{
    protected virtual bool IsPostgres => false;
    protected override void Up(MigrationBuilder m)
    {
        var dateType = IsPostgres ? "timestamp without time zone" : "datetime2";
        m.AddColumn<DateTime>("LeaveStart", "OperationalFleetStates", type: dateType, nullable: true);
        m.AddColumn<DateTime>("LeaveReturn", "OperationalFleetStates", type: dateType, nullable: true);
        m.AddColumn<string>("LeaveDriver", "OperationalFleetStates", maxLength: 250, nullable: true);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropColumn("LeaveStart", "OperationalFleetStates");
        m.DropColumn("LeaveReturn", "OperationalFleetStates");
        m.DropColumn("LeaveDriver", "OperationalFleetStates");
    }
}
