using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations.Postgres;
[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261028010000_FleetDriverAssignmentsPostgres")]
public sealed class FleetDriverAssignmentsPostgres : ITServiceDeskApp.Migrations.FleetDriverAssignments
{
    protected override bool IsPostgres => true;
}
