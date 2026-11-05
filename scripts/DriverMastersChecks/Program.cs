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
controller=Controller(managers[1]);
Assert(await controller.ConductorEstado(id,false,default) is ForbidResult && valid.IsActive,"Other driver editors cannot change activation state");
controller=Controller(managers[0]);
Assert(await controller.ConductorEstado(id,false,default) is RedirectToActionResult && !valid.IsActive,"Roldan can deactivate");
Assert(db.OperationalMasterRecords.Count()==1 && valid.Id==id,"Deactivation retains record and ID");
controller=Controller("leslieantonioespinoza@trawzacons.com");
Assert(await controller.ConductorEstado(id,true,default) is RedirectToActionResult && valid.IsActive,"Leslie can reactivate through the legacy driver endpoint");
Assert(await controller.ConductorEditar(id,default) is ForbidResult,"Unauthorized edit rejected");
controller=Controller(managers[3]);
Assert(await controller.ConductorEstado(id,false,default) is ForbidResult && valid.IsActive,"Other driver editors cannot deactivate through the legacy endpoint");
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
foreach(var kind in OperationalMasterCatalog.Kinds){
 var item=new OperationalMasterRecord{Kind=kind,Name="STATE "+kind,RecordKey="state:"+kind,Owner="Test",Tenure="Propio"};db.Add(item);db.SaveChanges();var originalId=item.Id;var originalKey=item.RecordKey;
 foreach(var email in new[]{"admin@trawzacons.com","roldanzamora@trawzacons.com","leslieantonioespinoza@trawzacons.com","Espinozaleslieantonioespinoza@trawzacons.com"}){
  Assert(await Controller(email).MaestroEstado(item.Id,false,default) is RedirectToActionResult && !item.IsActive,email+" deactivates "+kind);
  Assert((await Controller(email).Maestros(kind,status:"inactivos") as ViewResult)?.Model is IReadOnlyList<OperationalMasterRecord> rows && rows.Any(x=>x.Id==item.Id) && rows.All(x=>!x.IsActive),"Inactive records remain discoverable for "+kind);
  Assert(await Controller(email).MaestroEstado(item.Id,true,default) is RedirectToActionResult && item.IsActive,email+" activates "+kind);
  Assert(item.Id==originalId && item.RecordKey==originalKey,"Reintegration preserves master identity");
  var http=new DefaultHttpContext{User=Principal(email)};http.Request.Method="POST";var routes=new RouteData();routes.Values["controller"]="Operaciones";
  var filter=new AuthorizationFilterContext(new ActionContext(http,routes,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"action","MaestroEstado"}}}),[]);
  new AccessProfileAuthorizationFilter().OnAuthorization(filter);Assert(filter.Result==null,"Global authorization permits master status action for "+email);
 }
 foreach(var email in new[]{"monitoreo2@trawzacons.com","monitoreo@trawzacons.com","yerlinrocha@trawzacons.com"})
  Assert(await Controller(email).MaestroEstado(item.Id,false,default) is ForbidResult && item.IsActive,"Only designated users may change state: "+email);
}
Assert(await Controller("admin@trawzacons.com").MaestroEstado(int.MaxValue,false,default) is NotFoundResult,"Unknown IDs cannot be changed");
Assert(typeof(OperacionesController).GetMethod("MaestroEstado")!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute),true).Length==1,"All master state changes require CSRF protection");
Console.WriteLine("PASS: master activation/deactivation for administrators, Roldan and Leslie across all three catalogs; inactive filters, preserved IDs, restricted permissions and existing driver management.");
sealed class TestTempDataProvider : ITempDataProvider {
 public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();
 public void SaveTempData(HttpContext context,IDictionary<string,object> values){}
}
