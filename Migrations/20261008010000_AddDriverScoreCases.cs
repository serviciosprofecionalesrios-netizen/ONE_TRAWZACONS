using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261008010000_AddDriverScoreCases")]
public sealed class AddDriverScoreCases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable("DriverScoreCases", columns: t => new {
        EventKey = t.Column<string>(maxLength:64, nullable:false), Status = t.Column<string>(maxLength:30, nullable:false),
        Responsible = t.Column<string>(maxLength:150, nullable:true), Notes = t.Column<string>(maxLength:1000, nullable:true),
        Evidence = t.Column<byte[]>(nullable:true), EvidenceName = t.Column<string>(maxLength:255, nullable:true),
        UpdatedAt = t.Column<DateTime>(nullable:false), UpdatedBy = t.Column<string>(maxLength:150, nullable:true)
    }, constraints: t => t.PrimaryKey("PK_DriverScoreCases", x => x.EventKey));
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("DriverScoreCases");
}
