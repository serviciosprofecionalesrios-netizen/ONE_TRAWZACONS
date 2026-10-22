using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Security;
using ITServiceDeskApp.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
ClaimsPrincipal Person(string email,int id,Guid? session=null,string role="Administrator") {
 var claims=new List<Claim>{new(ClaimTypes.Email,email),new(ClaimTypes.Name,"User "+id),new("UserId",id.ToString()),new(ClaimTypes.Role,role)};
 if(session.HasValue)claims.Add(new(SiteActivityPolicy.SessionClaim,session.Value.ToString("N")));
 return new(new ClaimsIdentity(claims,"check"));
}
var clock=new ManualClock();
var services=new ServiceCollection().AddLogging().AddSingleton<TimeProvider>(clock).AddDbContext<ApplicationDbContext>(o=>o.UseInMemoryDatabase("activity-"+TestId.Value)).AddScoped<SiteActivityRecorder>();
using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var recorder=scope.ServiceProvider.GetRequiredService<SiteActivityRecorder>();
db.Users.AddRange(new User{Id=1,Email="admin@trawzacons.com",FullName="Owner",Phone="1",Department="IT",Role=UserRole.Administrator},new User{Id=2,Email="jzamora@trawzacons.com",FullName="Other admin",Phone="2",Department="IT",Role=UserRole.Administrator});db.SaveChanges();
var id=Guid.NewGuid();var owner=Person("admin@trawzacons.com",1,id);var other=Person("jzamora@trawzacons.com",2);
Check(SiteActivityPolicy.CanView(owner)&&!SiteActivityPolicy.CanView(other),"Exclusive owner, other administrators denied");
Check(!SiteActivityPolicy.CanView(Person("admin@trawzacons.com.attacker",2)),"Email suffix cannot bypass owner policy");
Check(!SiteActivityPolicy.CanView(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Email,"admin@trawzacons.com")}))),"Unauthenticated owner claim rejected");
Check(await recorder.Record(id,owner,"Inicio de sesión","Account","Login","Inicio de sesión",302),"Login recorded");
Check(await recorder.Pulse(id,owner,true,true),"First heartbeat accepted");clock.Advance(30);await recorder.Pulse(id,owner,true,true);
long Seconds()=>db.SiteActivitySessions.AsNoTracking().Single(x=>x.Id==id).ActiveSeconds;
Check(Seconds()==30,"Thirty seconds credited using server time");clock.Advance(1);await recorder.Pulse(id,owner,true,true);Check(Seconds()==30,"Rapid duplicate heartbeat does not inflate time");
clock.Advance(600);await recorder.Pulse(id,owner,true,true);Check(Seconds()==30,"Long disconnected interval not counted");
clock.Advance(30);await recorder.Pulse(id,owner,false,false);Check(Seconds()==30,"Hidden inactive tab adds no active time");
Check(!await recorder.Pulse(id,other,true,true),"Another user cannot modify a session");
SiteActivityController Controller(ClaimsPrincipal user)=>new(db,recorder,clock){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=user}}};
Check(await Controller(other).Index() is ForbidResult && await Controller(other).Snapshot() is ForbidResult,"Both full report and snapshot reject another administrator");
var report=(SiteActivityViewModel)((ViewResult)await Controller(owner).Index()).Model!;
Check(report.Users.Single(x=>x.Id==1).Status=="Ausente" && report.Online==1,"Hidden connected user shown absent");
clock.Advance(30);await recorder.Pulse(id,owner,true,true);clock.Advance(30);await recorder.Pulse(id,owner,true,true);
report=(SiteActivityViewModel)((ViewResult)await Controller(owner).Index()).Model!;
Check(Seconds()==60 && report.Users.Single(x=>x.Id==1).Status=="En línea","Visible recently active user online");
clock.Advance(91);report=(SiteActivityViewModel)((ViewResult)await Controller(owner).Index()).Model!;Check(report.Online==0,"Presence expires after missing heartbeats");
await recorder.Close(id,owner);Check(!await recorder.Pulse(id,owner,true,true),"Logged out session cannot be resurrected");
Check(db.SiteActivityEvents.Count(x=>x.Kind=="Cierre de sesión")==1,"Logout event recorded");
// Record a successful page response without storing any form, query or password content.
var nextId=Guid.NewGuid();var http=new DefaultHttpContext{User=Person("admin@trawzacons.com",1,nextId)};http.Request.Method="GET";http.Request.QueryString=new QueryString("?password=NEVER_RECORD_THIS");
var route=new RouteData();route.Values["controller"]="Operaciones";route.Values["action"]="Dashboard";
var actionContext=new ActionContext(http,route,new ActionDescriptor());var view=new ViewResult();var filters=new List<IFilterMetadata>();
var context=new ResultExecutingContext(actionContext,filters,view,new object());
await new SiteActivityFilter(recorder).OnResultExecutionAsync(context,()=>Task.FromResult(new ResultExecutedContext(actionContext,filters,view,new object())));
Check(db.SiteActivityEvents.Count(x=>x.Kind=="Página visitada")==1 && !db.SiteActivityEvents.Any(x=>x.Title.Contains("NEVER_RECORD_THIS")),"Page recorded without query contents");
var before=db.SiteActivityEvents.Count();route.Values["controller"]="SiteActivity";route.Values["action"]="Pulse";http.Request.Method="POST";
await new SiteActivityFilter(recorder).OnResultExecutionAsync(context,()=>Task.FromResult(new ResultExecutedContext(actionContext,filters,view,new object())));
Check(db.SiteActivityEvents.Count()==before,"Heartbeat does not generate navigation events");
// Unrelated unsaved changes in the calling context must not be committed by tracking.
db.Users.Add(new User{Id=3,Email="unsaved@example.com",FullName="Pending",Phone="3",Department="IT"});
await recorder.Record(nextId,owner,"Página visitada","Hs","Index","H&S",200);
Check(db.Users.AsNoTracking().Count()==2,"Tracking uses an isolated context, preserving business transaction boundaries");db.ChangeTracker.Clear();
foreach(var email in new[]{"roldanzamora@trawzacons.com","monitoreo@trawzacons.com","leslieantonioespinoza@trawzacons.com","ecanales@trawzacons.com","jzamora@trawzacons.com"}) {
 foreach(var action in new[]{"Pulse","Index","Snapshot"}) {
  var request=new DefaultHttpContext{User=Person(email,2)};request.Request.Method=action=="Pulse"?"POST":"GET";var routes=new RouteData();routes.Values["controller"]="SiteActivity";
  var ctx=new AuthorizationFilterContext(new ActionContext(request,routes,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"action",action}}}),new List<IFilterMetadata>());
  new AccessProfileAuthorizationFilter().OnAuthorization(ctx);Check((ctx.Result==null)==(action=="Pulse"),email+" "+action+" scope");
 }
}
// Local date filtering includes UTC-6 day boundaries and excludes the prior day.
db.SiteActivityEvents.AddRange(new SiteActivityEvent{SessionId=nextId,UserId=1,OccurredAtUtc=new DateTime(2026,10,22,5,59,0),Kind="Prior day",Title="Prior day"},new SiteActivityEvent{SessionId=nextId,UserId=1,OccurredAtUtc=new DateTime(2026,10,23,5,59,0),Kind="Selected day",Title="Selected day"});db.SaveChanges();
report=(SiteActivityViewModel)((ViewResult)await Controller(owner).Index(new DateTime(2026,10,22),new DateTime(2026,10,22),1)).Model!;
Check(report.Events.Any(x=>x.Kind=="Selected day")&&!report.Events.Any(x=>x.Kind=="Prior day"),"UTC-6 report date boundaries");
var attrs=typeof(SiteActivityController).GetMethod("Pulse")!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute),true);Check(attrs.Length==1,"Heartbeat requires anti-forgery token");
using var pg=new PostgresApplicationDbContext(new DbContextOptionsBuilder<PostgresApplicationDbContext>().UseNpgsql("Host=localhost;Database=check;Username=check;Password=unused").Options);
var pgScript=pg.GetService<IMigrator>().GenerateScript("20261020020000_DriverMasterManagementPostgres","20261022010000_SiteActivityPostgres");Check(pgScript.Contains("GENERATED BY DEFAULT AS IDENTITY")&&pgScript.Contains("timestamp without time zone")&&pgScript.Contains("SiteActivitySessions"),"PostgreSQL migration and identity");
using var sql=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=localhost;Database=check;Integrated Security=True;TrustServerCertificate=True").Options);
Check(sql.GetService<IMigrator>().GenerateScript("20261020020000_DriverMasterManagement","20261022010000_SiteActivity").Contains("IDENTITY"),"SQL Server migration");
Console.WriteLine("PASS: private activity report, ownership, presence expiry, conservative active time, isolated logging, no sensitive request contents and provider migrations.");
static class TestId {public static readonly string Value=Guid.NewGuid().ToString();}
sealed class ManualClock : TimeProvider {private DateTimeOffset now=new(2026,10,22,15,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>now;public void Advance(int seconds)=>now=now.AddSeconds(seconds);}
