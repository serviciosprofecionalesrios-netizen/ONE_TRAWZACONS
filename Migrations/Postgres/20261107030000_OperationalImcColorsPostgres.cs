using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations.Postgres;

[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261107030000_OperationalImcColorsPostgres")]
public sealed class OperationalImcColorsPostgres : OperationalImcColors
{
}
