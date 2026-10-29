using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261029010000_OperationalBreakdowns")]
public class OperationalBreakdowns : Migration
{
    protected virtual bool IsPostgres => false;
    protected override void Up(MigrationBuilder m)
    {
        var dateType=IsPostgres ? "timestamp without time zone" : "datetime2";
        m.CreateTable("OperationalBreakdowns",columns:t=>new {
            Id=t.Column<int>(nullable:false).Annotation("SqlServer:Identity","1, 1").Annotation("Npgsql:ValueGenerationStrategy",Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            SourceKey=t.Column<string>(maxLength:100,nullable:true),EventAt=t.Column<DateTime>(type:dateType,nullable:false),
            Equipment=t.Column<string>(maxLength:160,nullable:false),Driver=t.Column<string>(maxLength:160,nullable:true),Circuit=t.Column<string>(maxLength:80,nullable:false),
            EquipmentStatus=t.Column<string>(maxLength:80,nullable:false),Category=t.Column<string>(maxLength:80,nullable:false),Element=t.Column<string>(maxLength:200,nullable:false),
            DamageTarget=t.Column<string>(maxLength:80,nullable:false),Observation=t.Column<string>(maxLength:4000,nullable:true),Status=t.Column<string>(maxLength:80,nullable:false),
            EndAt=t.Column<DateTime>(type:dateType,nullable:true),SourceJson=t.Column<string>(nullable:true),CreatedAtUtc=t.Column<DateTime>(type:dateType,nullable:false),RegisteredBy=t.Column<string>(maxLength:150,nullable:false)
        },constraints:t=>t.PrimaryKey("PK_OperationalBreakdowns",x=>x.Id));
        m.CreateIndex("IX_OperationalBreakdowns_SourceKey","OperationalBreakdowns","SourceKey",unique:true,filter:IsPostgres?null:"[SourceKey] IS NOT NULL");
    }
    protected override void Down(MigrationBuilder m)=>m.DropTable("OperationalBreakdowns");
}
