using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261121010000_BreakdownMaintenanceOrders")]
public class BreakdownMaintenanceOrders : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        migration.AddColumn<int>("MaintenanceTicketId", "OperationalBreakdowns", nullable: true);
        migration.CreateIndex("IX_OperationalBreakdowns_MaintenanceTicketId", "OperationalBreakdowns", "MaintenanceTicketId");
        migration.AddForeignKey("FK_OperationalBreakdowns_Tickets_MaintenanceTicketId",
            "OperationalBreakdowns", "MaintenanceTicketId", "Tickets", principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migration)
    {
        migration.DropForeignKey("FK_OperationalBreakdowns_Tickets_MaintenanceTicketId", "OperationalBreakdowns");
        migration.DropIndex("IX_OperationalBreakdowns_MaintenanceTicketId", "OperationalBreakdowns");
        migration.DropColumn("MaintenanceTicketId", "OperationalBreakdowns");
    }
}
