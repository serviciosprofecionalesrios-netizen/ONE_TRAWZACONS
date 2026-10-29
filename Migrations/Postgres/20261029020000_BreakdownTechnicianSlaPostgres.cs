using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations.Postgres;
[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261029020000_BreakdownTechnicianSlaPostgres")]
public sealed class BreakdownTechnicianSlaPostgres : ITServiceDeskApp.Migrations.BreakdownTechnicianSla
{
    protected override bool IsPostgres=>true;
}
