using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations.Postgres;

[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261121010000_BreakdownMaintenanceOrdersPostgres")]
public sealed class BreakdownMaintenanceOrdersPostgres : BreakdownMaintenanceOrders
{
}
