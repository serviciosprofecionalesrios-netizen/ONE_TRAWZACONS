using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITServiceDeskApp.Migrations.Postgres
{
    [DbContext(typeof(PostgresApplicationDbContext))]
    [Migration("20260930090000_PersistOperacionesProductionGoals")]
    public partial class PersistOperacionesProductionGoals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperacionesMetasProduccionMensual",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    MetaMensualTriton = table.Column<decimal>(type: "numeric(18,1)", nullable: false),
                    MetaDiariaTritonObjetivo = table.Column<decimal>(type: "numeric(18,1)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_OperacionesMetasProduccionMensual", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_OperacionesMetasProduccionMensual_Year_Month", table: "OperacionesMetasProduccionMensual", columns: new[] { "Year", "Month" }, unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "OperacionesMetasProduccionMensual");
    }
}
