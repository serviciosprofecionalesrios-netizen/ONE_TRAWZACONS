using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
namespace ITServiceDeskApp.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261022010000_SiteActivity")]
public class SiteActivity : Migration
{
    protected virtual bool IsPostgres => false;
    protected override void Up(MigrationBuilder m)
    {
        var timestamp=IsPostgres ? "timestamp without time zone" : "datetime2";
        m.CreateTable("SiteActivitySessions",columns:t=>new {
            Id=t.Column<Guid>(nullable:false),UserId=t.Column<int>(nullable:false),
            Email=t.Column<string>(maxLength:150,nullable:false),DisplayName=t.Column<string>(maxLength:120,nullable:false),
            StartedAtUtc=t.Column<DateTime>(type:timestamp,nullable:false),LastSeenAtUtc=t.Column<DateTime>(type:timestamp,nullable:false),
            LastActivityAtUtc=t.Column<DateTime>(type:timestamp,nullable:true),LastPulseAtUtc=t.Column<DateTime>(type:timestamp,nullable:true),ClosedAtUtc=t.Column<DateTime>(type:timestamp,nullable:true),
            ActiveSeconds=t.Column<long>(nullable:false),IsVisible=t.Column<bool>(nullable:false),HasRecentActivity=t.Column<bool>(nullable:false),
            LastController=t.Column<string>(maxLength:80,nullable:false),LastAction=t.Column<string>(maxLength:80,nullable:false)
        },constraints:t=>t.PrimaryKey("PK_SiteActivitySessions",x=>x.Id));
        m.CreateIndex("IX_SiteActivitySessions_UserId_StartedAtUtc","SiteActivitySessions",new[]{"UserId","StartedAtUtc"});
        m.CreateIndex("IX_SiteActivitySessions_LastSeenAtUtc","SiteActivitySessions","LastSeenAtUtc");
        m.CreateTable("SiteActivityEvents",columns:t=>new {
            Id=t.Column<long>(nullable:false).Annotation(IsPostgres ? "Npgsql:ValueGenerationStrategy" : "SqlServer:Identity",IsPostgres ? (object)NpgsqlValueGenerationStrategy.IdentityByDefaultColumn : "1, 1"),
            SessionId=t.Column<Guid>(nullable:false),UserId=t.Column<int>(nullable:false),OccurredAtUtc=t.Column<DateTime>(type:timestamp,nullable:false),
            Kind=t.Column<string>(maxLength:30,nullable:false),Controller=t.Column<string>(maxLength:80,nullable:false),Action=t.Column<string>(maxLength:80,nullable:false),
            Title=t.Column<string>(maxLength:140,nullable:false),ResponseStatus=t.Column<int>(nullable:false)
        },constraints:t=>t.PrimaryKey("PK_SiteActivityEvents",x=>x.Id));
        m.CreateIndex("IX_SiteActivityEvents_OccurredAtUtc","SiteActivityEvents","OccurredAtUtc");
        m.CreateIndex("IX_SiteActivityEvents_UserId_OccurredAtUtc","SiteActivityEvents",new[]{"UserId","OccurredAtUtc"});
    }
    protected override void Down(MigrationBuilder m) {m.DropTable("SiteActivityEvents");m.DropTable("SiteActivitySessions");}
}
