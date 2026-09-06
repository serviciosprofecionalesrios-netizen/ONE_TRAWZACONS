using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceCostCentersCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinanceCostCenters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Area = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CostCenterNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CostCenterName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MonthlyBudgetCordoba = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AlertThresholdPercent = table.Column<int>(type: "int", nullable: false),
                    HardStopOnOverrun = table.Column<bool>(type: "bit", nullable: false),
                    RequireAuthorizationOnOverrun = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceCostCenters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCostCenters_Area_IsActive",
                table: "FinanceCostCenters",
                columns: new[] { "Area", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCostCenters_CostCenterNumber",
                table: "FinanceCostCenters",
                column: "CostCenterNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceCostCenters");
        }
    }
}
