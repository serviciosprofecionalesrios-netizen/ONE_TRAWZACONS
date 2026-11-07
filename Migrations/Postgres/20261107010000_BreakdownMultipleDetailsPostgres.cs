using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations.Postgres;

[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261107010000_BreakdownMultipleDetailsPostgres")]
public sealed class BreakdownMultipleDetailsPostgres : BreakdownMultipleDetails
{
}
