using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations.Postgres;

[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261107020000_OperationalImcClearancesPostgres")]
public sealed class OperationalImcClearancesPostgres : OperationalImcClearances
{
}
