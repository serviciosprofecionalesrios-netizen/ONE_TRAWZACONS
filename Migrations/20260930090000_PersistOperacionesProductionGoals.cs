using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260930090000_PersistOperacionesProductionGoals")]
    public partial class PersistOperacionesProductionGoals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperacionesMetasProduccionMensual",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    MetaMensualTriton = table.Column<decimal>(type: "decimal(18,1)", nullable: false),
                    MetaDiariaTritonObjetivo = table.Column<decimal>(type: "decimal(18,1)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_OperacionesMetasProduccionMensual", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_OperacionesMetasProduccionMensual_Year_Month", table: "OperacionesMetasProduccionMensual", columns: new[] { "Year", "Month" }, unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "OperacionesMetasProduccionMensual");
    }
}
