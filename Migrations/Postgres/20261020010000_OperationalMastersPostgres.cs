using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
namespace ITServiceDeskApp.Migrations.Postgres;
[DbContext(typeof(PostgresApplicationDbContext))]
[Migration("20261020010000_OperationalMastersPostgres")]
public sealed class OperationalMastersPostgres : OperationalMasters
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("OperationalMasterRecords", columns: t => new {
            Id = t.Column<int>(nullable:false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Kind = t.Column<string>(maxLength:20, nullable:false), Name = t.Column<string>(maxLength:160, nullable:false),
            Plate = t.Column<string>(maxLength:30, nullable:true), Tenure = t.Column<string>(maxLength:30, nullable:false),
            Owner = t.Column<string>(maxLength:180, nullable:false), RecordKey = t.Column<string>(maxLength:250, nullable:false), CreatedAt = t.Column<DateTime>(type:"timestamp without time zone", nullable:false)
        }, constraints: t => t.PrimaryKey("PK_OperationalMasterRecords", x => x.Id));
        migrationBuilder.CreateIndex("IX_OperationalMasterRecords_RecordKey", "OperationalMasterRecords", "RecordKey", unique:true);
    }
}
