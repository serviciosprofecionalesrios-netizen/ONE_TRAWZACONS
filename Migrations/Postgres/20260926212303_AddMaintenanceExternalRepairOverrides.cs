using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITServiceDeskApp.Migrations.Postgres;

public partial class AddMaintenanceExternalRepairOverrides : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MaintenanceExternalRepairs",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SourceRepairNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AssignedMechanic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                WorkPerformed = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                DeliveryDocumentPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_MaintenanceExternalRepairs", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceExternalRepairs_SourceRepairNumber",
            table: "MaintenanceExternalRepairs",
            column: "SourceRepairNumber",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "MaintenanceExternalRepairs");
}
