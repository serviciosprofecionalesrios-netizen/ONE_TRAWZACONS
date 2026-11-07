using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace ITServiceDeskApp.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261107010000_BreakdownMultipleDetails")]
public class BreakdownMultipleDetails : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        migration.AddColumn<string>("AssignedTechnicianIds", "OperationalBreakdowns", nullable: true);
        migration.AddColumn<bool>("EquipmentUnderRepair", "OperationalBreakdowns", nullable: false, defaultValue: false);
        migration.AddColumn<bool>("GondolaUnderRepair", "OperationalBreakdowns", nullable: false, defaultValue: false);
        migration.CreateTable("OperationalBreakdownDamage", columns: table => new
        {
            Id = table.Column<int>(nullable: false)
                .Annotation("SqlServer:Identity", "1, 1")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            OperationalBreakdownId = table.Column<int>(nullable: false),
            Category = table.Column<string>(maxLength: 80, nullable: false),
            Element = table.Column<string>(maxLength: 200, nullable: false),
            Target = table.Column<string>(maxLength: 80, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_OperationalBreakdownDamage", row => row.Id);
            table.ForeignKey("FK_OperationalBreakdownDamage_OperationalBreakdowns_OperationalBreakdownId",
                row => row.OperationalBreakdownId, "OperationalBreakdowns", "Id", onDelete: ReferentialAction.Cascade);
        });
        migration.CreateIndex("IX_OperationalBreakdownDamage_OperationalBreakdownId", "OperationalBreakdownDamage", "OperationalBreakdownId");
    }

    protected override void Down(MigrationBuilder migration)
    {
        migration.DropTable("OperationalBreakdownDamage");
        migration.DropColumn("AssignedTechnicianIds", "OperationalBreakdowns");
        migration.DropColumn("EquipmentUnderRepair", "OperationalBreakdowns");
        migration.DropColumn("GondolaUnderRepair", "OperationalBreakdowns");
    }
}
