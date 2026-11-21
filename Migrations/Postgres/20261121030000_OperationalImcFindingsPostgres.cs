using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations.Postgres;

[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261121030000_OperationalImcFindingsPostgres")]
public sealed class OperationalImcFindingsPostgres : OperationalImcFindings
{
}
