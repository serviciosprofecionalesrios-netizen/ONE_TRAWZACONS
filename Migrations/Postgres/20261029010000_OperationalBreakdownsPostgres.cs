using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations.Postgres;
[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261029010000_OperationalBreakdownsPostgres")]
public sealed class OperationalBreakdownsPostgres : ITServiceDeskApp.Migrations.OperationalBreakdowns
{
    protected override bool IsPostgres=>true;
}
