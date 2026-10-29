using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261029020000_BreakdownTechnicianSla")]
public class BreakdownTechnicianSla : Migration
{
    protected virtual bool IsPostgres=>false;
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<int>("AssignedTechnicianId","OperationalBreakdowns",nullable:true);
        m.AddColumn<DateTime>("SlaDeadline","OperationalBreakdowns",type:IsPostgres?"timestamp without time zone":"datetime2",nullable:true);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropColumn("AssignedTechnicianId","OperationalBreakdowns");m.DropColumn("SlaDeadline","OperationalBreakdowns");
    }
}
