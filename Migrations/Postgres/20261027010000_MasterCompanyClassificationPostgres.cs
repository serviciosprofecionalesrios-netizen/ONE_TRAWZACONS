using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ITServiceDeskApp.Migrations.Postgres;
[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261027010000_MasterCompanyClassificationPostgres")]
public sealed class MasterCompanyClassificationPostgres : MasterCompanyClassification { }
