using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Security;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Operaciones;
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

void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
var options=new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
using var db=new ApplicationDbContext(options);
var store=new OperacionesSeguimientoStore(db);
var day=new DateTime(2026,10,27);
SeguimientoToneladasRowViewModel Row(string unit,string state,int offset=0,string driver="Conductor actual")=>new(){Equipo=unit,Estado=state,FechaEvento=day.AddDays(offset),Procedencia="TRITON",Ruta="TRITON - MINA",Conductor=driver,Toneladas=30,Viajes=1};
SeguimientoToneladasViewModel Upload(params SeguimientoToneladasRowViewModel[] rows)=>new(){Rows=rows.ToList(),FechaOperativa=rows.Max(x=>x.FechaEvento),IsFromUpload=true,SourceFileName="TiHouse.xlsx"};
var truck1=new OperationalMasterRecord{Kind="equipos",Name="W-0001",Plate="M100001",Owner="Owner",Tenure="Propio",RecordKey="truck1"};
var truck2=new OperationalMasterRecord{Kind="equipos",Name="W-0002",Plate="M100002",Owner="Owner",Tenure="Propio",RecordKey="truck2"};
var holcim=new OperationalMasterRecord{Kind="equipos",Name="NM900001",Owner="Pending",Tenure="Pending",RecordKey="holcim",CompanyClassification="HOLCIM"};
db.AddRange(truck1,truck2,holcim,new OperationalMasterRecord{Kind="gondolas",Name="G-01",Owner="Owner",Tenure="Propio",RecordKey="trailer"});db.SaveChanges();
FleetBoardViewModel Board()=>OperationalFleetBoard.Build(db.OperationalMasterRecords.ToList(),store.Get()??new(),db.OperationalFleetStates.AsNoTracking().ToList());
Check(Board().Equipment.Count==3 && Board().Equipment.All(x=>x.Status=="standby"),"All equipment, including Holcim, starts Standby without a real upload; no trailers");
store.Save(Upload(Row("W-0001","EN RUTA",-1),Row("W-0002","FINALIZADO",-1)));
store.Save(Upload(Row("W-0001","EN RUTA"),Row("M-100001","FINALIZADO",0,"Conductor anterior"),Row("W-0001","FINALIZADO",0,"Último conductor"),Row("UNKNOWN","EN RUTA")));
var source=store.Get()!;var board=Board();
Check(source.Rows.Any(x=>x.Equipo=="W-0002") && !source.LatestUploadEquipment.Contains("W-0002"),"Historical rows preserved but excluded from latest upload membership");
Check(board.Equipment.Single(x=>x.Code=="W-0002").Status=="standby","Historical-only equipment is Standby");
Check(board.Equipment.Single(x=>x.Key=="master:"+truck1.Id).Status=="finalizado" && board.Equipment.Count(x=>x.Key=="master:"+truck1.Id)==1,"One latest state per master; plate/code aliases do not duplicate equipment");
Check(board.Equipment.Single(x=>x.Key=="master:"+truck1.Id).Driver=="Último conductor","The last source row supplies the actual driver across plate/code aliases");
Check(board.Equipment.Any(x=>x.Code=="UNKNOWN"&&x.Status=="ruta") && board.Equipment.Count==4,"TiHouse equipment not yet in master remains visible");
Check(OperationalFleetBoard.Build(db.OperationalMasterRecords.ToList(),new(){Rows=[Row("W-0001","EN RUTA")]},[]).Equipment.All(x=>x.Status=="standby"),"Sample rows never activate equipment");

ClaimsPrincipal Person(string email,string? profile=null)=>new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Email,email),new Claim(ClaimTypes.Role,"Administrator")}.Concat(profile==null?[]:new[]{new Claim(UserAccessProfiles.ClaimType,profile)}),"test"));
OperacionesController Controller(string email="monitoreo@trawzacons.com",string? profile=null){
 var http=new DefaultHttpContext{User=Person(email,profile)};
 return new OperacionesController(null!,store,null!,null!,null!,null!,db,null!,null!,NullLogger<OperacionesController>.Instance,null!,null!){ControllerContext=new ControllerContext{HttpContext=http},TempData=new TempDataDictionary(http,new TempData())};
}
var key="master:"+truck1.Id;
foreach(var reason in OperationalFleetBoard.Reasons){
 Check(await Controller().ActualizarEstadoEquipo(key,"fuera-ruta",reason,Board().LoadVersion,default) is RedirectToActionResult,"Save reason: "+reason);
 Check(Board().Equipment.Single(x=>x.Key==key).Reason==reason && Board().Equipment.Single(x=>x.Key==key).Status=="fuera-ruta","Saved reason reloads from database");
}
Check(await Controller().ActualizarEstadoEquipo(key,"fuera-ruta",null,Board().LoadVersion,default) is BadRequestResult,"Missing reason rejected");
Check(await Controller().ActualizarEstadoEquipo(key,"fuera-ruta","Invalid",Board().LoadVersion,default) is BadRequestResult,"Unknown reason rejected");
Check(await Controller().ActualizarEstadoEquipo("master:99999","carretera",null,Board().LoadVersion,default) is BadRequestResult,"Cannot modify an unknown equipment ID");
var priorVersion=Board().LoadVersion;
store.Save(Upload(Row("W-0001","EN RUTA",1,"Nuevo conductor")));
Check(Board().Equipment.Single(x=>x.Key==key).Status=="fuera-ruta" && Board().Equipment.Single(x=>x.Key==key).Reason=="Conductor de Pase","New TiHouse upload cannot reactivate or erase a manual outside-route reason");
using(var reopened=new ApplicationDbContext(options)) {
 var reloaded=OperationalFleetBoard.Build(reopened.OperationalMasterRecords.ToList(),new OperacionesSeguimientoStore(reopened).Get()!,reopened.OperationalFleetStates.ToList());
 Check(reloaded.Equipment.Single(x=>x.Key==key).Status=="fuera-ruta","A new request/context retains manual outside-route status");
}
store.Save(Upload(Row("W-0002","EN RUTA",2)));
Check(Board().Equipment.Single(x=>x.Key==key).Status=="fuera-ruta" && !Board().Equipment.Single(x=>x.Key==key).InLatestUpload,"Outside-route equipment stays classified even when absent from next file");
var stale=Controller();Check(await stale.ActualizarEstadoEquipo(key,"auto",null,priorVersion,default) is RedirectToActionResult && stale.TempData.ContainsKey("FleetError") && Board().Equipment.Single(x=>x.Key==key).Status=="fuera-ruta","Stale activation request cannot act on a different upload");
await Controller().ActualizarEstadoEquipo(key,"auto",null,Board().LoadVersion,default);
Check(Board().Equipment.Single(x=>x.Key==key).Status=="standby" && !db.OperationalFleetStates.Any(),"Only manual activation clears classification; absent equipment returns to Standby");
await Controller().ActualizarEstadoEquipo("master:"+truck2.Id,"carretera",null,Board().LoadVersion,default);
store.Save(Upload(Row("W-0002","FINALIZADO",3)));
Check(Board().Equipment.Single(x=>x.Code=="W-0002").Status=="carretera","New uploads also preserve manual roadside/workshop classifications");
await Controller().ActualizarEstadoEquipo("master:"+truck2.Id,"auto",null,Board().LoadVersion,default);
Check(Board().Equipment.Single(x=>x.Code=="W-0002").Status=="finalizado","Manual activation returns present equipment to latest TiHouse state");
store.Save(Upload(Row("UNKNOWN","EN RUTA",4)));
await Controller().ActualizarEstadoEquipo("unit:UNKNOWN","fuera-ruta","Otros",Board().LoadVersion,default);
store.Save(Upload(Row("W-0002","EN RUTA",5)));
Check(Board().Equipment.Single(x=>x.Code=="UNKNOWN").Status=="fuera-ruta","Unknown-master equipment also retains manual status after disappearing from file");
var registered=new OperationalMasterRecord{Kind="equipos",Name="UNKNOWN",Plate="M900099",Owner="Owner",Tenure="Propio",RecordKey="later"};
db.Add(registered);db.Add(new OperationalFleetState{EquipmentKey="unit:M900099",EquipmentCode="M900099",Status="fuera-ruta",Reason="Otros",UpdatedAtUtc=DateTime.UtcNow});db.SaveChanges();
Check(Board().Equipment.Single(x=>x.Code=="UNKNOWN").ManualStateKeys.Count==2,"Previously unknown code/plate classifications converge to one new master");
await Controller().ActualizarEstadoEquipo("master:"+registered.Id,"auto",null,Board().LoadVersion,default);
Check(!db.OperationalFleetStates.Any() && Board().Equipment.Single(x=>x.Code=="UNKNOWN").Status=="standby","Manual activation clears all aliases of the same equipment");
Check(db.OperationalMasterRecords.All(x=>x.IsActive),"Standby and status changes do not deactivate master records");
foreach(var email in new[]{"admin@trawzacons.com","roldanzamora@trawzacons.com","monitoreo2@trawzacons.com","monitoreo@trawzacons.com"}){
 Check(OperationalFleetBoard.CanManage(Person(email)),"Existing operations manager can save: "+email);
 var http=new DefaultHttpContext{User=Person(email)};http.Request.Method="POST";var route=new RouteData();route.Values["controller"]="Operaciones";
 var auth=new AuthorizationFilterContext(new ActionContext(http,route,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"action","ActualizarEstadoEquipo"}}}),[]);
 new AccessProfileAuthorizationFilter().OnAuthorization(auth);Check(auth.Result==null,"Global filter allows the same narrow status action");
}
Check(await Controller("supervisor@example.com",UserAccessProfiles.SupervisorOperations).ActualizarEstadoEquipo(key,"carretera",null,Board().LoadVersion,default) is ForbidResult,"Read-only operations supervisor cannot mutate equipment state");
Check(typeof(OperacionesController).GetMethod("ActualizarEstadoEquipo")!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute),true).Length==1,"State changes require CSRF protection");
var mixed=Upload(Row("W-0002","EN RUTA",6));var holcimRow=Row(holcim.Name,"EN RUTA",6);holcimRow.Procedencia="HOLCIM";holcimRow.Ruta="HOLCIM - DESTINO";
mixed.LatestUploadRows=mixed.Rows.Append(holcimRow).ToList();store.Save(mixed);
Check(store.Get()!.LatestUploadEquipment.Contains(holcim.Name) && Board().Equipment.Single(x=>x.Code==holcim.Name).Status=="ruta","Equipment presence uses the whole uploaded TiHouse, including other companies/sites");
Check(store.Get()!.Rows.All(x=>x.Procedencia=="TRITON"),"Fleet membership does not change the production-tonnage scope");
// Legacy upload metadata without the new snapshot can still recover exact last-batch membership.
var metadata=db.OperacionesSeguimientoCargas.Single();metadata.EquipmentSnapshotJson=null;db.SaveChanges();
Check(store.Get()!.LatestUploadEquipment.SequenceEqual(new[]{"W-0002"}),"Legacy metadata recovers latest batch, not all historical units");
using var pg=new PostgresApplicationDbContext(new DbContextOptionsBuilder<PostgresApplicationDbContext>().UseNpgsql("Host=localhost;Database=check;Username=check;Password=unused").Options);
Check(pg.GetService<IMigrator>().GenerateScript("20261027010000_MasterCompanyClassificationPostgres","20261027020000_OperationalFleetPostgres").Contains("timestamp without time zone"),"PostgreSQL fleet migration generated");
using var sql=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=localhost;Database=check;Integrated Security=True;TrustServerCertificate=True").Options);
Check(sql.GetService<IMigrator>().GenerateScript("20261027010000_MasterCompanyClassification","20261027020000_OperationalFleet").Contains("EquipmentSnapshotJson"),"SQL Server fleet migration generated");
Console.WriteLine("PASS: latest-upload membership, complete master coverage, persistent manual status across uploads, explicit activation, reasons, permissions and migrations.");
sealed class TempData : ITempDataProvider {public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();public void SaveTempData(HttpContext context,IDictionary<string,object> values){}}
