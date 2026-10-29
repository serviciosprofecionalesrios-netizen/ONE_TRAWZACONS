using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.Security;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
var data=OperationalBreakdownCatalog.Missing([]);
Check(data.Count==1607,"All 1607 source rows imported, without invented records");
Check(data.Select(x=>x.SourceKey).Distinct().Count()==1607,"Every source row has an independent identity, including repeated events");
Check(OperationalBreakdownCatalog.Missing(data.Select(x=>x.SourceKey)).Count==0,"Reimport is idempotent");
Check(data.All(x=>x.Circuit=="Limón" && x.SourceJson!=null),"Original columns retained and source circuit preserved");
Check(OperationalBreakdownCatalog.Rows.Where(x=>x.Get("Fecha Fin Evento").Length>0).All(x=>OperationalBreakdownCatalog.Date(x.Get("Fecha Fin Evento"))!=null),"All nonempty closing dates parsed");
foreach(var row in data)foreach(var property in typeof(OperationalBreakdown).GetProperties()){
 var max=(System.ComponentModel.DataAnnotations.MaxLengthAttribute?)Attribute.GetCustomAttribute(property,typeof(System.ComponentModel.DataAnnotations.MaxLengthAttribute));
 if(max!=null && property.GetValue(row) is string text && text.Length>max.Length)throw new Exception("Length overflow: "+property.Name+" "+row.SourceKey);
}
using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
OperationalBreakdownCatalog.Seed(db);OperationalBreakdownCatalog.Seed(db);
Check(db.OperationalBreakdowns.Count()==1607,"Startup import is idempotent in isolated database");
var truck=new OperationalMasterRecord{Kind="equipos",Name="W-0001",RecordKey="test-truck"};
db.Add(truck);db.SaveChanges();
var store=new OperacionesSeguimientoStore(db);
OperacionesController Controller(string? profile=null){
 var claims=new List<Claim>{new(ClaimTypes.Email,profile==null?"monitoreo@trawzacons.com":"supervisor@example.com"),new(ClaimTypes.Role,"Administrator")};
 if(profile!=null)claims.Add(new(UserAccessProfiles.ClaimType,profile));
 var http=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(claims,"test"))};
 return new OperacionesController(null!,store,null!,null!,null!,null!,db,null!,null!,NullLogger<OperacionesController>.Instance,null!,null!){ControllerContext=new ControllerContext{HttpContext=http},TempData=new TempDataDictionary(http,new TempData())};
}
var form=(BreakdownCreateViewModel)((ViewResult)Controller().NuevaAveria("master:"+truck.Id)).Model!;
Check(form.Equipment==truck.Name && form.Circuit=="Limón","Truck menu preselects equipment and defaults circuit to Limón");
var category=OperationalBreakdownCatalog.Elements.First();
BreakdownCreateViewModel Valid()=>new(){Equipment=truck.Name,EquipmentStatus="Vacio",Category=category.Key,Element=category.Value.First(),DamageTarget="EQUIPO"};
Check(await Controller().NuevaAveria(Valid(),default) is RedirectToActionResult,"Authorized user can register new breakdown");
Check(db.OperationalBreakdowns.Count()==1608 && db.OperationalBreakdowns.OrderBy(x=>x.Id).Last().Status=="Abierta","Manual record is saved as open and historical count preserved");
var invalid=Valid();invalid.Element="INVALID";
Check(await Controller().NuevaAveria(invalid,default) is ViewResult && db.OperationalBreakdowns.Count()==1608,"Unrelated damage element rejected without saving");
Check(await Controller(UserAccessProfiles.SupervisorOperations).NuevaAveria(Valid(),default) is ForbidResult,"Read-only supervisor cannot create breakdowns");
Check(((BreakdownListViewModel)((ViewResult)await Controller().Averias(null,1)).Model!).Records.Count==50,"Historical table paginates at 50 rows");
using var pg=new PostgresApplicationDbContext(new DbContextOptionsBuilder<PostgresApplicationDbContext>().UseNpgsql("Host=localhost;Database=check;Username=check;Password=unused").Options);
Check(pg.GetService<IMigrator>().GenerateScript("20261028010000_FleetDriverAssignmentsPostgres","20261029010000_OperationalBreakdownsPostgres").Contains("OperationalBreakdowns"),"PostgreSQL migration script generated without connecting");
using var sql=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=localhost;Database=check;Integrated Security=True;TrustServerCertificate=True").Options);
Check(sql.GetService<IMigrator>().GenerateScript("20261028010000_FleetDriverAssignments","20261029010000_OperationalBreakdowns").Contains("OperationalBreakdowns"),"SQL Server migration script generated without connecting");
Console.WriteLine("PASS: Averias acceptance checks complete");
sealed class TempData:ITempDataProvider{public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>();public void SaveTempData(HttpContext c,IDictionary<string,object> v){}}


