using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations.Postgres;
[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261022010000_SiteActivityPostgres")]
public sealed class SiteActivityPostgres : SiteActivity { protected override bool IsPostgres => true; }
