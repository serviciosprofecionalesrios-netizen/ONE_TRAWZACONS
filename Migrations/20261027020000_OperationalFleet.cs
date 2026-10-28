using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261027020000_OperationalFleet")]
public class OperationalFleet : Migration
{
    protected virtual bool IsPostgres => false;
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>("EquipmentSnapshotJson","OperacionesSeguimientoCargas",nullable:true);
        m.CreateTable("OperationalFleetStates",columns:t=>new {
            EquipmentKey=t.Column<string>(maxLength:200,nullable:false),EquipmentCode=t.Column<string>(maxLength:160,nullable:false),Status=t.Column<string>(maxLength:30,nullable:false),
            Reason=t.Column<string>(maxLength:50,nullable:true),SourceUploadedAt=t.Column<DateTime>(type:IsPostgres ? "timestamp without time zone" : "datetime2",nullable:true),
            UpdatedAtUtc=t.Column<DateTime>(type:IsPostgres ? "timestamp without time zone" : "datetime2",nullable:false),ChangedBy=t.Column<string>(maxLength:150,nullable:false)
        },constraints:t=>t.PrimaryKey("PK_OperationalFleetStates",x=>x.EquipmentKey));
    }
    protected override void Down(MigrationBuilder m) {m.DropTable("OperationalFleetStates");m.DropColumn("EquipmentSnapshotJson","OperacionesSeguimientoCargas");}
}
