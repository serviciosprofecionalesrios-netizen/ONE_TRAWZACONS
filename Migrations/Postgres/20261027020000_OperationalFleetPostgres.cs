using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations.Postgres;
[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261027020000_OperationalFleetPostgres")]
public sealed class OperationalFleetPostgres : OperationalFleet { protected override bool IsPostgres => true; }
