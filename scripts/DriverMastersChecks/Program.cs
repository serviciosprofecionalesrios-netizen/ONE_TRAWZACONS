using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Security;
using ITServiceDeskApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
void Assert(bool pass, string message) { if (!pass) throw new Exception(message); }
ClaimsPrincipal Principal(string email) => new(new ClaimsIdentity(new[] {new Claim(ClaimTypes.Email,email), new Claim(ClaimTypes.Role,"Administrator")}, "test"));
var managers = new[]{"roldanzamora@trawzacons.com","monitoreo2@trawzacons.com","monitoreo2@trawzacions.com","monitoreo@trawzacons.com","admin@trawzacons.com"};
foreach(var email in managers) {
 var user=Principal(email); Assert(UserAccessProfiles.CanMaintainDrivers(user), "Driver manager "+email);
 foreach(var action in new[]{"MaestroNuevo","MaestroImportar","ConductorEditar","ConductorEstado"}) {
  var http=new DefaultHttpContext{User=user}; http.Request.Method="POST";
  var routes=new RouteData(); routes.Values["controller"]="Operaciones";
  var descriptor=new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"action",action}}};
  var filter=new AuthorizationFilterContext(new ActionContext(http,routes,descriptor),new List<IFilterMetadata>());
  new AccessProfileAuthorizationFilter().OnAuthorization(filter); Assert(filter.Result==null, email+" "+action);
 }
}
Assert(!UserAccessProfiles.CanMaintainMaster(Principal(managers[0]),"equipos"),"Roldan cannot maintain trucks");
foreach(var email in new[]{"leslieantonioespinoza@trawzacons.com","yerlinrocha@trawzacons.com","coordinadorlaliberta@trawzacons.com"}) Assert(!UserAccessProfiles.CanMaintainDrivers(Principal(email)),"Other profiles cannot maintain drivers");
using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
OperacionesController Controller(string email) {
 var http=new DefaultHttpContext{User=Principal(email)};
 return new OperacionesController(null!,null!,null!,null!,null!,null!,db,null!,null!,NullLogger<OperacionesController>.Instance,null!,null!) { ControllerContext=new ControllerContext {HttpContext=http}, TempData=new TempDataDictionary(http,new TestTempDataProvider()) };
}

var controller=Controller(managers[0]);
var invalid=new OperationalMasterRecord{Kind="conductores",FirstName="Juan",FirstSurname="Perez",Tenure="Propio",Owner="TRAWZACONS"};
Assert(await controller.MaestroNuevo(invalid,default) is ViewResult && !controller.ModelState.IsValid && !db.OperationalMasterRecords.Any(),"Incomplete name rejected without saving");
controller=Controller(managers[0]);
var valid=new OperationalMasterRecord{Kind="conductores",FirstName=" José ",SecondName="Antonio",FirstSurname="Pérez",SecondSurname="López",Tenure="Propio",Owner="TRAWZACONS"};
Assert(await controller.MaestroNuevo(valid,default) is RedirectToActionResult,"Roldan can create driver");
var id=valid.Id; Assert(id>0 && valid.Name=="José Antonio Pérez López" && valid.DisplayId == $"TW-{id:D6}","Stable ID and composed full name");
controller=Controller(managers[1]);
var duplicate=new OperationalMasterRecord{Kind="conductores",FirstName="JOSE",SecondName=" ANTONIO ",FirstSurname="PEREZ",SecondSurname="LOPEZ",Tenure="Propio",Owner="other"};
Assert(await controller.MaestroNuevo(duplicate,default) is ViewResult && !controller.ModelState.IsValid && db.OperationalMasterRecords.Count()==1,"Normalized duplicate rejected");
controller=Controller(managers[2]);
Assert(await controller.ConductorEstado(id,false,default) is RedirectToActionResult && !valid.IsActive,"Hellen can deactivate");
Assert(db.OperationalMasterRecords.Count()==1 && valid.Id==id,"Deactivation retains record and ID");
controller=Controller("leslieantonioespinoza@trawzacons.com");
Assert(await controller.ConductorEstado(id,true,default) is ForbidResult && !valid.IsActive,"Unauthorized reactivation rejected");
Assert(await controller.ConductorEditar(id,default) is ForbidResult,"Unauthorized edit rejected");
controller=Controller(managers[3]);
Assert(await controller.ConductorEstado(id,true,default) is RedirectToActionResult && valid.IsActive,"Gabriela can reactivate");
var edited=new OperationalMasterRecord{FirstName="José",SecondName="Alberto",FirstSurname="Pérez",SecondSurname="López",Tenure="Propio",Owner="TRAWZACONS"};
Assert(await controller.ConductorEditar(id,edited,default) is RedirectToActionResult && valid.Id==id && valid.Name=="José Alberto Pérez López","Edit preserves ID");
controller=Controller(managers[0]);
Assert(controller.MaestroNuevo("equipos") is ForbidResult,"Shared endpoint cannot escalate Roldan to truck maintenance");
Assert(await controller.MaestroImportar("gondolas",null,default) is ForbidResult,"Bulk endpoint scope restricted");
OperationalMasterCatalog.Seed(db);
var seeded=db.OperationalMasterRecords.First(x=>x.Kind=="conductores" && x.SourceKey!=null);
var sourceKey=seeded.SourceKey; var seedId=seeded.Id; var count=db.OperationalMasterRecords.Count();
seeded.Name="Nombre Corregido Para Prueba";seeded.RecordKey=OperationalMasterCatalog.Key("conductores",seeded.Name);seeded.IsActive=false; db.SaveChanges();
OperationalMasterCatalog.Seed(db);
Assert(db.OperationalMasterRecords.Count()==count && !seeded.IsActive && seeded.Id==seedId,"Restart cannot resurrect renamed/deactivated source records");
Assert((await Controller(managers[0]).Maestros("conductores",null,1,"inactivos",default) as ViewResult)?.Model is IReadOnlyList<OperationalMasterRecord> inactive && inactive.All(x=>!x.IsActive),"Inactive filter");
// Exercise the real bulk endpoint: invalid batches are atomic, valid batches skip duplicates.
var beforeBulk = db.OperationalMasterRecords.Count();
var template = (FileContentResult)Controller(managers[0]).MaestroPlantilla("conductores");
OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
using (var excel = new OfficeOpenXml.ExcelPackage(new MemoryStream(template.FileContents))) {
 var sheet=excel.Workbook.Worksheets[0];
 var values=new[]{"Pedro","Miguel","Gonzalez","Ruiz","Propio","TRAWZACONS"};
 for(var c=0;c<values.Length;c++)sheet.Cells[2,c+2].Value=values[c];
 sheet.Cells[3,2].Value="Nombre incompleto";
 IFormFile FileOf(byte[] bytes)=>new FormFile(new MemoryStream(bytes),0,bytes.Length,"archivo","conductores.xlsx");
 var bulk=Controller(managers[0]);await bulk.MaestroImportar("conductores",FileOf(excel.GetAsByteArray()),default);
 Assert(db.OperationalMasterRecords.Count()==beforeBulk && bulk.TempData.ContainsKey("MasterError"),"Invalid bulk batch saves no partial rows");
 sheet.DeleteRow(3);bulk=Controller(managers[0]);await bulk.MaestroImportar("conductores",FileOf(excel.GetAsByteArray()),default);
 Assert(db.OperationalMasterRecords.Count()==beforeBulk+1 && bulk.TempData.ContainsKey("MasterOk"),"Roldan can bulk add complete names");
 await Controller(managers[0]).MaestroImportar("conductores",FileOf(excel.GetAsByteArray()),default);
 Assert(db.OperationalMasterRecords.Count()==beforeBulk+1,"Repeat bulk skips normalized duplicates");
}
Assert(db.OperationalMasterRecords.Select(x=>x.Id).Distinct().Count()==db.OperationalMasterRecords.Count(),"Every master record has a distinct ID");
foreach(var field in new[]{"FirstName","SecondName","FirstSurname","SecondSurname"}) {
 var row=new OperationalMasterRecord{FirstName="Juan",SecondName="Carlos",FirstSurname="Perez",SecondSurname="Lopez"};
 typeof(OperationalMasterRecord).GetProperty(field)!.SetValue(row," ");Assert(OperationalMasterCatalog.PrepareDriver(row)!=null,"Each of four name fields is mandatory: "+field);
}
using var pg=new PostgresApplicationDbContext(new DbContextOptionsBuilder<PostgresApplicationDbContext>().UseNpgsql("Host=localhost;Database=check;Username=check;Password=unused").Options);
var pgScript=pg.GetService<IMigrator>().GenerateScript("20261020010000_OperationalMastersPostgres","20261020020000_DriverMasterManagementPostgres");
Assert(pgScript.Contains("boolean NOT NULL DEFAULT TRUE") && pgScript.Contains("SourceKey") && pgScript.Contains("SecondSurname"),"Postgres migration");
using var ss=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=localhost;Database=check;Integrated Security=True;TrustServerCertificate=True").Options);
Assert(ss.GetService<IMigrator>().GenerateScript("20261020010000_OperationalMasters","20261020020000_DriverMasterManagement").Contains("IsActive"),"SQL Server migration");
Console.WriteLine("PASS: named-user permissions, narrowed truck scope, mandatory four-part names, accent/space/case duplicates, create/edit/deactivate/reactivate, immutable IDs, source restart safety, status filters and both migrations.");
sealed class TestTempDataProvider : ITempDataProvider {
 public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();
 public void SaveTempData(HttpContext context,IDictionary<string,object> values){}
}
