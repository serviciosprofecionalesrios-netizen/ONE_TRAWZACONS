using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261020010000_OperationalMasters")]
public class OperationalMasters : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("OperationalMasterRecords", columns: t => new {
            Id = t.Column<int>(nullable:false).Annotation("SqlServer:Identity", "1, 1"),
            Kind = t.Column<string>(maxLength:20, nullable:false), Name = t.Column<string>(maxLength:160, nullable:false),
            Plate = t.Column<string>(maxLength:30, nullable:true), Tenure = t.Column<string>(maxLength:30, nullable:false),
            Owner = t.Column<string>(maxLength:180, nullable:false), RecordKey = t.Column<string>(maxLength:250, nullable:false), CreatedAt = t.Column<DateTime>(nullable:false)
        }, constraints: t => t.PrimaryKey("PK_OperationalMasterRecords", x => x.Id));
        migrationBuilder.CreateIndex("IX_OperationalMasterRecords_RecordKey", "OperationalMasterRecords", "RecordKey", unique:true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("OperationalMasterRecords");
}
