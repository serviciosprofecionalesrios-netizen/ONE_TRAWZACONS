using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations.Postgres;
[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261124010000_FleetLeaveDatesPostgres")]
public sealed class FleetLeaveDatesPostgres : ITServiceDeskApp.Migrations.FleetLeaveDates
{
    protected override bool IsPostgres => true;
}
