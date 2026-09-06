using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSparePartDispatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketSparePartDispatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    MaintenanceInventoryPartId = table.Column<int>(type: "int", nullable: false),
                    PartCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PartName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    QuantityDispatched = table.Column<int>(type: "int", nullable: false),
                    UnitCostCordoba = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalCostCordoba = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitCostUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalCostUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSparePartDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketSparePartDispatches_MaintenanceInventoryParts_MaintenanceInventoryPartId",
                        column: x => x.MaintenanceInventoryPartId,
                        principalTable: "MaintenanceInventoryParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketSparePartDispatches_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketSparePartDispatches_MaintenanceInventoryPartId",
                table: "TicketSparePartDispatches",
                column: "MaintenanceInventoryPartId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSparePartDispatches_TicketId",
                table: "TicketSparePartDispatches",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketSparePartDispatches");
        }
    }
}
