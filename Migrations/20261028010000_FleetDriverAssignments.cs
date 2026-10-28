using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261028010000_FleetDriverAssignments")]
public class FleetDriverAssignments : Migration
{
    protected virtual bool IsPostgres => false;
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("OperationalFleetDrivers", columns: t => new {
            EquipmentKey = t.Column<string>(maxLength:200, nullable:false),
            EquipmentCode = t.Column<string>(maxLength:160, nullable:false),
            DriverId = t.Column<int>(nullable:false),
            UpdatedAtUtc = t.Column<DateTime>(type:IsPostgres ? "timestamp without time zone" : "datetime2", nullable:false),
            ChangedBy = t.Column<string>(maxLength:150, nullable:false)
        }, constraints: t => t.PrimaryKey("PK_OperationalFleetDrivers", x => x.EquipmentKey));
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("OperationalFleetDrivers");
}
