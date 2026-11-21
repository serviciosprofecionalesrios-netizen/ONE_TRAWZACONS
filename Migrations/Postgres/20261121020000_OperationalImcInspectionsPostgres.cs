using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITServiceDeskApp.Migrations.Postgres;

[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261121020000_OperationalImcInspectionsPostgres")]
public sealed class OperationalImcInspectionsPostgres : OperationalImcInspections
{
}
