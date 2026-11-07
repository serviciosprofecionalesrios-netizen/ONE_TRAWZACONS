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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Rendering;

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
FleetBoardViewModel Board()=>OperationalFleetBoard.Build(db.OperationalMasterRecords.ToList(),store.Get()??new(),db.OperationalFleetStates.AsNoTracking().ToList(),db.OperationalFleetDrivers.AsNoTracking().ToList());
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
Check(OperationalFleetBoard.Build([truck2],new(){IsFromUpload=true,LatestUploadAt=day,LatestUploadEquipment=[truck2.Name],Rows=[Row(truck2.Name,"NO RECONOCIDO")]},[]).Equipment.Single().Status=="standby","Unrecognized source states go to Standby, without an Other states card");

ClaimsPrincipal Person(string email,string? profile=null)=>new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Email,email),new Claim(ClaimTypes.Role,"Administrator")}.Concat(profile==null?[]:new[]{new Claim(UserAccessProfiles.ClaimType,profile)}),"test"));
OperacionesController Controller(string email="monitoreo@trawzacons.com",string? profile=null){
 var http=new DefaultHttpContext{User=Person(email,profile)};
 return new OperacionesController(null!,store,null!,null!,null!,null!,db,null!,null!,NullLogger<OperacionesController>.Instance,null!,null!){ControllerContext=new ControllerContext{HttpContext=http},TempData=new TempDataDictionary(http,new TempData())};
}
var key="master:"+truck1.Id;
foreach(var email in new[]{"leslieantonioespinoza@trawzacons.com","Espinozaleslieantonioespinoza@trawzacons.com"}){
 Check(!OperationalFleetBoard.CanManage(Person(email)),"Leslie retains existing unrelated fleet permissions");
 foreach(var state in new[]{"taller-psb","taller-pct","finalizado"}){
  Check(await Controller(email).ActualizarEstadoEquipo(key,state,null,Board().LoadVersion,default) is RedirectToActionResult,"Leslie can move equipment: "+state);
  Check(Board().Equipment.Single(x=>x.Key==key).Status==state,"Leslie's equipment state persists: "+state);
 }
 var http=new DefaultHttpContext{User=Person(email)};http.Request.Method="POST";var route=new RouteData();route.Values["controller"]="Operaciones";
 var auth=new AuthorizationFilterContext(new ActionContext(http,route,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"action","ActualizarEstadoEquipo"}}}),[]);
 new AccessProfileAuthorizationFilter().OnAuthorization(auth);Check(auth.Result==null,"Global filter permits Leslie's equipment status changes");
}
Check(!OperationalFleetBoard.CanChangeStatus(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Email,"leslieantonioespinoza@trawzacons.com")}))),"Unauthenticated Leslie identity cannot change states");
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
await Controller().ActualizarEstadoEquipo(key,"finalizado",null,Board().LoadVersion,default);
Check(Board().Equipment.Single(x=>x.Key==key).Status=="finalizado" && !Board().Equipment.Single(x=>x.Key==key).InLatestUpload,"Standby can be manually activated as discharged without a TiHouse row");
store.Save(Upload(Row("W-0002","EN RUTA",2)));
Check(Board().Equipment.Single(x=>x.Key==key).Status=="finalizado","Manual activation survives uploads that still omit the equipment");
store.Save(Upload(Row("W-0001","EN RUTA",2)));
Check(Board().Equipment.Single(x=>x.Key==key).Status=="ruta","An actual later TiHouse trip updates manually activated equipment");
await Controller().ActualizarEstadoEquipo(key,"auto",null,Board().LoadVersion,default);
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
var driver1 = new OperationalMasterRecord { Kind="conductores",Name="Juan Carlos Perez Ruiz",RecordKey="driver1" };
var driver2 = new OperationalMasterRecord { Kind="conductores",Name="Pedro Jose Lopez Cruz",RecordKey="driver2" };
var inactiveDriver = new OperationalMasterRecord { Kind="conductores",Name="Inactivo",RecordKey="inactive",IsActive=false };
db.AddRange(driver1,driver2,inactiveDriver);db.SaveChanges();
Check(await Controller().AsignarConductorEquipo(key,driver1.Id,Board().LoadVersion,default) is RedirectToActionResult,"Assign an active catalog driver to Standby equipment");
Check(Board().Equipment.Single(x=>x.Key==key).Driver==driver1.Name && Board().Equipment.Single(x=>x.Key==key).Status=="standby","Driver assignment preserves equipment status");
await Controller().ActualizarEstadoEquipo(key,"finalizado",null,Board().LoadVersion,default);
await Controller().AsignarConductorEquipo(key,driver2.Id,Board().LoadVersion,default);
Check(Board().Equipment.Single(x=>x.Key==key).Driver==driver2.Name && Board().Equipment.Single(x=>x.Key==key).Status=="finalizado","Change driver on discharged equipment");
store.Save(Upload(Row("W-0001","EN RUTA",8,"Conductor TiHouse")));
Check(Board().Equipment.Single(x=>x.Key==key).Driver==driver2.Name && Board().Equipment.Single(x=>x.Key==key).Status=="ruta","Manual driver survives TiHouse upload while actual trip changes status");
await Controller().AsignarConductorEquipo(key,driver1.Id,Board().LoadVersion,default);
Check(Board().Equipment.Single(x=>x.Key==key).Driver==driver1.Name,"Change driver while equipment is en route");
Check(await Controller().AsignarConductorEquipo(key,inactiveDriver.Id,Board().LoadVersion,default) is BadRequestResult,"Inactive driver rejected");
Check(await Controller().AsignarConductorEquipo(key,truck1.Id,Board().LoadVersion,default) is BadRequestResult,"Equipment cannot be assigned as a driver");
Check(await Controller().AsignarConductorEquipo("master:999999",driver1.Id,Board().LoadVersion,default) is BadRequestResult,"Unknown equipment rejected for driver assignment");
Check(await Controller("supervisor@example.com",UserAccessProfiles.SupervisorOperations).AsignarConductorEquipo(key,driver1.Id,Board().LoadVersion,default) is ForbidResult,"Read-only supervisor cannot assign a driver");
var savedDriverId=db.OperationalFleetDrivers.Single(x=>x.EquipmentKey==key).DriverId;
await Controller().AsignarConductorEquipo(key,driver2.Id,"stale",default);
Check(db.OperationalFleetDrivers.Single(x=>x.EquipmentKey==key).DriverId==savedDriverId,"Stale upload prevents driver assignment");
Check(typeof(OperacionesController).GetMethod("AsignarConductorEquipo")!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute),true).Length==1,"Driver assignment requires CSRF protection");
foreach(var email in new[]{"admin@trawzacons.com","roldanzamora@trawzacons.com","monitoreo2@trawzacons.com","monitoreo@trawzacons.com"}){
 var http=new DefaultHttpContext{User=Person(email)};http.Request.Method="POST";var route=new RouteData();route.Values["controller"]="Operaciones";
 var auth=new AuthorizationFilterContext(new ActionContext(http,route,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"action","AsignarConductorEquipo"}}}),[]);
 new AccessProfileAuthorizationFilter().OnAuthorization(auth);Check(auth.Result==null,"Global filter allows driver assignment: "+email);
}
Check(pg.GetService<IMigrator>().GenerateScript("20261027020000_OperationalFleetPostgres","20261028010000_FleetDriverAssignmentsPostgres").Contains("OperationalFleetDrivers"),"PostgreSQL assignment migration generated without connecting to database");
Check(sql.GetService<IMigrator>().GenerateScript("20261027020000_OperationalFleet","20261028010000_FleetDriverAssignments").Contains("OperationalFleetDrivers"),"SQL Server assignment migration generated without connecting to database");
Console.WriteLine("PASS: fleet activation, driver assignments, upload persistence, permissions and migration scripts.");
// Regression: a manual location must not erase TiHouse's load color.
var colorUnits = new[]{("W-0067","taller-psb"),("W-0102","taller-psb"),("W-0089","carretera")};
var colorSource = new SeguimientoToneladasViewModel {
 IsFromUpload=true, LatestUploadAt=day.AddDays(4), LatestUploadEquipment=colorUnits.Select(x=>x.Item1).ToList(),
 LatestUploadRows=colorUnits.Select(x=>Row(x.Item1,"EN RUTA",-60)).ToList()
};
var colorChanges=colorUnits.Select(x=>new OperationalFleetState{EquipmentKey="unit:"+HolcimMasterCatalog.Identifier(x.Item1),EquipmentCode=x.Item1,Status=x.Item2,UpdatedAtUtc=day}).ToList();
var colorBoard=OperationalFleetBoard.Build([],colorSource,colorChanges);
var renderBuilder=Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions{ApplicationName=typeof(OperacionesController).Assembly.FullName});
Microsoft.Extensions.DependencyInjection.MvcServiceCollectionExtensions.AddControllersWithViews(renderBuilder.Services).AddApplicationPart(typeof(OperacionesController).Assembly);
await using var renderApp=renderBuilder.Build();
using var renderScope=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(renderApp.Services);
var razor=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.AspNetCore.Mvc.Razor.IRazorViewEngine>(renderScope.ServiceProvider);
Task<string> RenderChip(FleetEquipmentViewModel equipment) => RenderView(equipment,"/Views/Operaciones/_FleetEquipmentChip.cshtml");
async Task<string> RenderView<T>(T model, string path) {
 var http=new DefaultHttpContext{RequestServices=renderScope.ServiceProvider};
 var renderRoutes=new RouteData(); renderRoutes.Routers.Add(new RouteCollection()); renderRoutes.Values["controller"]="Operaciones";
 var action=new ActionContext(http,renderRoutes,new ActionDescriptor());
 var view=razor.GetView(null,path,false);
 Check(view.Success,"Actual fleet chip Razor view resolves");
 using var writer=new StringWriter();
 var data=new ViewDataDictionary<T>(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary()){Model=model};
 var context=new ViewContext(action,view.View!,data,new TempDataDictionary(http,new TempData()),writer,new HtmlHelperOptions());
 await view.View!.RenderAsync(context);
 return writer.ToString();
}
foreach(var (code,location) in colorUnits){
 var unit=colorBoard.Equipment.Single(x=>x.Code==code);
 Check(unit.Status==location && unit.BaseStatus=="ruta","Old pending load keeps manual location: "+code);
 var html=await RenderChip(unit);
 Check(html.Contains("equipo-chip equipo-chip--ruta dropdown-toggle") && !html.Contains("equipo-chip--descargado"),"Rendered chip is gold in PSB/road: "+code);
}
if(args.Length>0) {
 OfficeOpenXml.ExcelPackage.LicenseContext=OfficeOpenXml.LicenseContext.NonCommercial;
 using var package=new OfficeOpenXml.ExcelPackage(new FileInfo(args[0]));
 var parser=typeof(OperacionesController).GetMethod("ParseToneladasRowsFromWorkbook",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
 object?[] parseArgs=[package.Workbook,null,null];
 var actual=(List<SeguimientoToneladasRowViewModel>)parser.Invoke(null,parseArgs)!;
 var actualSource=new SeguimientoToneladasViewModel{IsFromUpload=true,LatestUploadAt=day,LatestUploadEquipment=actual.Select(x=>x.Equipo).ToList(),LatestUploadRows=actual};
 var actualBoard=OperationalFleetBoard.Build([],actualSource,colorChanges);
 foreach(var (code,location) in colorUnits) {
  var unit=actualBoard.Equipment.Single(x=>x.Code==code);
  Check(unit.Status==location && (await RenderChip(unit)).Contains("equipo-chip equipo-chip--ruta dropdown-toggle"),"Actual uploaded TiHouse workbook renders loaded color in manual location: "+code);
 }
}
var partialSource=new SeguimientoToneladasViewModel {
 IsFromUpload=true,LatestUploadAt=day,LatestUploadEquipment=["PARTIAL"],LatestUploadRows=[Row("PARTIAL","FINALIZADO")],
 Rows=colorUnits.Select(x=>Row(x.Item1,"EN RUTA",-60)).ToList()
};
var partialBoard=OperationalFleetBoard.Build([],partialSource,colorChanges);
foreach(var (code,location) in colorUnits){
 var unit=partialBoard.Equipment.Single(x=>x.Code==code);
 Check(!unit.InLatestUpload && unit.Status==location && (await RenderChip(unit)).Contains("equipo-chip equipo-chip--ruta dropdown-toggle"),"Partial upload retains historical loaded color in manual location: "+code);
}
partialSource.Rows[0].Estado="FINALIZADO";
Check((await RenderChip(OperationalFleetBoard.Build([],partialSource,colorChanges).Equipment.Single(x=>x.Code=="W-0067"))).Contains("equipo-chip equipo-chip--descargado dropdown-toggle"),"Historical FINALIZADO closes carried load without changing PSB");
colorSource.LatestUploadRows[0].Estado="FINALIZADO";
var discharged=OperationalFleetBoard.Build([],colorSource,colorChanges).Equipment.Single(x=>x.Code=="W-0067");
Check(discharged.Status=="taller-psb" && (await RenderChip(discharged)).Contains("equipo-chip equipo-chip--descargado dropdown-toggle"),"Actual FINALIZADO changes to blue while preserving PSB");

// A chip's load color and its active card must agree, even after partial uploads.
var cardUnits = new[] {
 new FleetEquipmentViewModel {Code="W-0114",Company="EQUINOX",Status="finalizado",HistoricalLoadStatus="ruta"},
 new FleetEquipmentViewModel {Code="W-0001",Company="EQUINOX",Status="standby",HistoricalLoadStatus="ruta"},
 new FleetEquipmentViewModel {Code="W-0025",Company="EQUINOX",Status="standby",HistoricalLoadStatus="finalizado"},
 new FleetEquipmentViewModel {Code="UNRECOGNIZED",Company="EQUINOX",Status="standby"},
 new FleetEquipmentViewModel {Code="W-0067",Company="EQUINOX",Status="taller-psb",HistoricalLoadStatus="ruta"},
 new FleetEquipmentViewModel {Code="W-0089",Company="EQUINOX",Status="carretera",HistoricalLoadStatus="ruta"},
 new FleetEquipmentViewModel {Code="OUTSIDE",Company="EQUINOX",Status="fuera-ruta",HistoricalLoadStatus="ruta",Reason="Otros"}
};
Check(cardUnits[0].CardStatus=="ruta" && cardUnits[1].CardStatus=="ruta", "Loaded units leave discharged and standby cards");
Check(cardUnits[2].CardStatus=="finalizado" && cardUnits[3].CardStatus=="standby", "Known discharged units leave standby; unknown units stay");
Check(cardUnits[4].CardStatus=="taller-psb" && cardUnits[5].CardStatus=="carretera" && cardUnits[6].CardStatus=="fuera-ruta", "Manual PSB, roadside and outside-route locations remain intact");
var cardsHtml=await RenderView(new FleetBoardViewModel {Equipment=cardUnits.ToList()},"/Views/Operaciones/_FleetBoard.cshtml");
var routeSection=cardsHtml.IndexOf("Equipos en ruta (2)",StringComparison.Ordinal);
var standbySection=cardsHtml.IndexOf("Equipos en Standby (1)",StringComparison.Ordinal);
var downloadedSection=cardsHtml.IndexOf("Equipos descargados (1)",StringComparison.Ordinal);
Check(downloadedSection>=0 && routeSection>downloadedSection && standbySection>routeSection,"Actual rendered card counts match load states");
Check(cardsHtml.IndexOf(">W-0114</button>",StringComparison.Ordinal)>routeSection && cardsHtml.IndexOf(">W-0114</button>",StringComparison.Ordinal)<standbySection && cardsHtml.IndexOf(">W-0001</button>",StringComparison.Ordinal)>routeSection && cardsHtml.IndexOf(">W-0001</button>",StringComparison.Ordinal)<standbySection,"Actual board renders loaded units only in route card");

// IMC classifications persist independently of TiHouse's load state.
foreach(var imc in new[]{"imc-roja","imc-amarilla"}) {
 Check(await Controller().ActualizarEstadoEquipo(key,imc,null,Board().LoadVersion,default) is RedirectToActionResult,"Save IMC classification: "+imc);
 var marked=Board().Equipment.Single(x=>x.Key==key);
 Check(marked.Status==imc && marked.DistributionStatus!="imc-roja" && marked.DistributionStatus!="imc-amarilla","Existing IMC classification keeps the unit visible in the operational distribution: "+imc);
 var imcHtml=await RenderView(new FleetBoardViewModel {Equipment=[marked]},"/Views/Operaciones/_FleetBoard.cshtml");
 Check(imcHtml.Contains("equipo-chip equipo-chip--"+imc+" dropdown-toggle"),"Existing manually colored chip still renders: "+imc);
 store.Save(Upload(Row(truck1.Name,"EN RUTA",10)));
 Check(Board().Equipment.Single(x=>x.Key==key).Status==imc,"IMC remains after a new TiHouse upload: "+imc);
}
Check(await Controller("supervisor@example.com",UserAccessProfiles.SupervisorOperations).ActualizarEstadoEquipo(key,"imc-roja",null,Board().LoadVersion,default) is ForbidResult,"Read-only supervisor cannot assign IMC");
var november=ImcHistory.ForMonth(null);
Check(november.SelectedMonth=="2026-11" && november.Months.Count==11 && november.Months.Last().Value=="2026-01",
 "IMC defaults to the latest month and retains January through November");
Check(november.Cards.Select(card=>card.Units.Count).SequenceEqual([2,1,3]),"November matches the three color counts in the workbook");
Check(await Controller().ActualizarEstadoEquipo(key,"imc-verde",null,Board().LoadVersion,default,"2026-11") is RedirectToActionResult,
 "Operator can mark a red IMC inspection green");
Check(await Controller().ActualizarEstadoEquipo(key,"imc-verde",null,Board().LoadVersion,default,"2026-11") is RedirectToActionResult &&
 db.OperationalImcClearances.Count()==1,"Repeating clearance does not duplicate the record");
var corrected=ImcHistory.ForMonth("2026-11",db.OperationalImcClearances.AsNoTracking().ToList());
Check(corrected.Cards.Select(card=>card.Units.Count).SequenceEqual([1,1,4]) &&
 corrected.Cards.Single(card=>card.ColorClass=="verde").Units.Contains(truck1.Name),"Clearance moves the unit from red to green in the monthly count");
Check(ImcHistory.ForMonth("2026-04",db.OperationalImcClearances.AsNoTracking().ToList()).Cards
 .Select(card=>card.Units.Count).SequenceEqual([3,0,11]),"A November clearance preserves the April history");
Check(await Controller().ActualizarEstadoEquipo(key,"finalizado",null,Board().LoadVersion,default,"2026-11") is RedirectToActionResult,
 "Operator can activate a cleared unit");
var clearedUnit=Board().Equipment.Single(x=>x.Key==key);
clearedUnit.ImcColor="verde";
Check(clearedUnit.ChipCssClass=="equipo-chip--ruta","Activated cleared unit displays the ordinary loaded color");
foreach (var (status, color) in new[] { ("imc-amarilla", "amarilla"), ("imc-roja", "roja"), ("imc-verde", "verde") })
{
 Check(await Controller().ActualizarEstadoEquipo(key,status,null,Board().LoadVersion,default,"2026-11") is RedirectToActionResult,
  $"A previously cleared unit can switch to {color}");
 var updated=ImcHistory.ForMonth("2026-11",db.OperationalImcClearances.AsNoTracking().ToList());
 Check(updated.Cards.Single(card=>card.Units.Contains(truck1.Name)).ColorClass==color &&
  db.OperationalImcClearances.Count()==1 && Board().Equipment.Single(x=>x.Key==key).DistributionStatus=="ruta",
  $"Changing IMC to {color} replaces the monthly color and preserves the load state");
}
Check(new FleetEquipmentViewModel {Status="finalizado",ImcColor="verde"}.ChipCssClass=="equipo-chip--descargado",
 "Cleared discharged unit displays the ordinary blue color");
Check(pg.GetService<IMigrator>().GenerateScript("20261107010000_BreakdownMultipleDetailsPostgres","20261107020000_OperationalImcClearancesPostgres").Contains("OperationalImcClearances") &&
 sql.GetService<IMigrator>().GenerateScript("20261107010000_BreakdownMultipleDetails","20261107020000_OperationalImcClearances").Contains("OperationalImcClearances"),
 "IMC clearance migration generates for both production and local database providers");
var novemberHtml=await RenderView(new FleetBoardViewModel {Imc=november},"/Views/Operaciones/_FleetBoard.cshtml");
Check(novemberHtml.Contains("equipo-chip--imc-roja\">W-0001") &&
 novemberHtml.Contains("equipo-chip--imc-amarilla\">H-05"),"November renders each listed unit in its own inspection color");
var loadedWithImc=new FleetEquipmentViewModel {Code="H-05",Status="ruta",BaseStatus="ruta",ImcColor="amarilla"};
Check(loadedWithImc.DistributionStatus=="ruta" && loadedWithImc.ChipCssClass=="equipo-chip--imc-amarilla",
 "A loaded yellow IMC unit stays in the route card and uses the distinct inspection color");
var april=ImcHistory.ForMonth("2026-04");
Check(april.Cards.Select(card=>card.Units.Count).SequenceEqual([3,0,11]) && april.Cards.Last().Units.Contains("W-0105"),
 "April uses each unit's latest inspection, including a unit that went from yellow to green");
var aprilHtml=await RenderView(new FleetBoardViewModel {Imc=april},"/Views/Operaciones/_FleetBoard.cshtml");
Check(aprilHtml.Contains("IMC (Roja) (3)") && aprilHtml.Contains("IMC (Amarilla) (0)") && aprilHtml.Contains("IMC (Verde) (11)") &&
 aprilHtml.Contains("W-0105"),"Historical selection renders the counts and the unit codes");
var damageNow=new DateTime(2026,10,31,18,30,0,DateTimeKind.Utc);
var damageMaster=new OperationalMasterRecord {Id=9001,Kind="equipos",Name="DAMAGE",Plate="M-123456",IsActive=true};
var damageRecords=new[]{
 new OperationalBreakdown {Id=701,Equipment="M123456",EventAt=new DateTime(2026,10,30,9,15,0),Category="Motor",Element="Radiador",DamageTarget="Equipo",Observation="Fuga <script>no ejecutar</script>"},
 new OperationalBreakdown {Id=702,Equipment="DAMAGE",EventAt=new DateTime(2026,10,31,10,0,0),Category="Frenos",Element="Bomba",DamageTarget="Góndola",Observation="Fuga <script>no ejecutar</script>"},
 new OperationalBreakdown {Id=703,Equipment="DAMAGE",EventAt=new DateTime(2026,10,31,12,0,0),Category="Cerrada",EndAt=new DateTime(2026,10,31,12,15,0),Status="Cerrada"},
 new OperationalBreakdown {Id=704,Equipment="DAMAGE",EventAt=new DateTime(2026,11,1),Category="Anulada",SourceJson="{\"VIGENCIA\":\"NULO\"}"}
};
foreach(var location in new[]{"carretera","taller-pct","taller-psb","taller-poz"}) {
 var damageBoard=OperationalFleetBoard.Build([damageMaster],new(),[new(){EquipmentKey="master:9001",EquipmentCode="DAMAGE",Status=location}],breakdowns:damageRecords.Where(x=>x.Id!=703),nowUtc:damageNow);
 var unit=damageBoard.Equipment.Single();
 Check(unit.Status==location && unit.Breakdowns.Count==1 && unit.Breakdowns[0].Id==702,"Only the latest active breakdown is shown for unit code and plate: "+location);
 Check(unit.Breakdowns.Single().ElapsedLabel=="0 d 2 h 30 min","Elapsed time uses Nicaragua event time against UTC clock");
 var damageHtml=await RenderChip(unit);
 Check(damageHtml.Contains("Bomba") && damageHtml.Contains("Frenos") && damageHtml.Contains("31/10/2026 10:00") && damageHtml.Contains("0 d 2 h 30 min") && !damageHtml.Contains("Radiador") && !damageHtml.Contains("30/10/2026 09:15") && damageHtml.Contains("data-breakdown-events="),"Tooltip and dropdown render damage, element, report time and elapsed duration: "+location);
 Check(!damageHtml.Contains("<script>no ejecutar</script>"),"Damage observations are HTML encoded");
}
var missingDamage=new FleetEquipmentViewModel {Code="NO-INCIDENT",Status="taller-psb"};
Check((await RenderChip(missingDamage)).Contains("Sin aver&#xED;a registrada."),"Missing incidents are explicitly identified instead of invented");
var closedLatest=OperationalFleetBoard.Build([damageMaster],new(),[new(){EquipmentKey="master:9001",EquipmentCode="DAMAGE",Status="taller-psb"}],breakdowns:damageRecords,nowUtc:damageNow).Equipment.Single();
Check(closedLatest.Breakdowns.Single().Id==703 && closedLatest.Breakdowns.Single().IsClosed && closedLatest.Breakdowns.Single().ElapsedLabel=="0 d 0 h 15 min","Latest valid closed incident replaces an older open one and uses actual closing time");
var closedLater=OperationalFleetBoard.Build([damageMaster],new(),[new(){EquipmentKey="master:9001",EquipmentCode="DAMAGE",Status="taller-psb"}],breakdowns:damageRecords,nowUtc:damageNow.AddDays(30)).Equipment.Single();
Check(closedLater.Breakdowns.Single().ElapsedLabel=="0 d 0 h 15 min","Closed-incident duration does not accumulate days after closing");
var closedHtml=System.Net.WebUtility.HtmlDecode(await RenderChip(closedLatest));
Check(closedHtml.Contains("Cerrada") && closedHtml.Contains("Duración de la avería:") && !closedHtml.Contains("data-breakdown-reported-at="),"Closed tooltip is explicitly identified and excluded from the live dropdown timer");
var newestOpen=new OperationalBreakdown{Id=705,Equipment="DAMAGE",EventAt=new DateTime(2026,10,31,12,20,0),Category="Nueva avería"};
var newest=OperationalFleetBoard.Build([damageMaster],new(),[new(){EquipmentKey="master:9001",EquipmentCode="DAMAGE",Status="taller-psb"}],breakdowns:damageRecords.Append(newestOpen),nowUtc:damageNow).Equipment.Single().Breakdowns.Single();
Check(newest.Id==705 && !newest.IsClosed && newest.ElapsedLabel=="0 d 0 h 10 min","A newer active incident replaces the earlier closed incident and starts its own elapsed time");

// Explicit unassignment overrides both a manual assignment and TiHouse's driver.
await Controller().AsignarConductorEquipo(key,driver1.Id,Board().LoadVersion,default);
var beforeUnassign=Board().Equipment.Single(x=>x.Key==key);
Check(await Controller().AsignarConductorEquipo(key,0,Board().LoadVersion,default,desasignar:true) is RedirectToActionResult,"Driver can be explicitly unassigned");
var unassigned=Board().Equipment.Single(x=>x.Key==key);
Check(unassigned.Driver=="" && unassigned.AssignedDriverId==null && unassigned.Status==beforeUnassign.Status,"Unassignment clears driver without changing equipment status");
store.Save(Upload(Row(truck1.Name,"EN RUTA",11,"Conductor del archivo")));
Check(Board().Equipment.Single(x=>x.Key==key).Driver=="","TiHouse upload cannot undo explicit driver removal");
var staleRemoval=Controller();
Check(await staleRemoval.AsignarConductorEquipo(key,0,"stale",default,desasignar:true) is RedirectToActionResult && staleRemoval.TempData.ContainsKey("FleetError"),"Stale unassignment is rejected through upload version check");
Check(await Controller("supervisor@example.com",UserAccessProfiles.SupervisorOperations).AsignarConductorEquipo(key,0,Board().LoadVersion,default,desasignar:true) is ForbidResult,"Read-only users cannot unassign drivers");
Check(await Controller().AsignarConductorEquipo("master:999999",0,Board().LoadVersion,default,desasignar:true) is BadRequestResult,"Unknown equipment cannot be unassigned");
Check(await Controller().AsignarConductorEquipo(key,0,Board().LoadVersion,default) is BadRequestResult,"Zero is not accepted as an ordinary driver assignment");
await Controller().AsignarConductorEquipo(key,driver2.Id,Board().LoadVersion,default);
Check(Board().Equipment.Single(x=>x.Key==key).Driver==driver2.Name,"A new driver can be assigned after explicit removal");

var loadedFigures=Row(truck1.Name,"EN RUTA",12);loadedFigures.PesoSugerido=32.8m;loadedFigures.Toneladas=31m;loadedFigures.CombustibleLitros=70m;
store.Save(Upload(loadedFigures));
var loadedUnit=Board().Equipment.Single(x=>x.Code==truck1.Name);
Check(loadedUnit.SuggestedWeight==32.8m && loadedUnit.CompletedTonnes==null,"Loaded tooltip uses suggested weight rather than final tonnage");
var loadedHtml=System.Net.WebUtility.HtmlDecode(await RenderChip(loadedUnit));
Check(loadedHtml.Contains("Peso sugerido:") && !loadedHtml.Contains("Toneladas finalizadas:"),"Loaded tooltip renders only its relevant load figure");
var completedFigures=Row(truck1.Name,"FINALIZADO",13);completedFigures.Toneladas=32.85m;completedFigures.CombustibleLitros=76.5m;
store.Save(Upload(completedFigures));
var completedUnit=Board().Equipment.Single(x=>x.Code==truck1.Name);
Check(completedUnit.CompletedTonnes==32.85m && completedUnit.DispatchedFuelGallons==76.5m && completedUnit.SuggestedWeight==null,"Discharged figures come from the latest completed trip, not prior accumulated trips");
var completedHtml=System.Net.WebUtility.HtmlDecode(await RenderChip(completedUnit));
Check(completedHtml.Contains("Toneladas finalizadas:") && completedHtml.Contains("Combustible despachado:") && completedHtml.Contains(" gal") && !completedHtml.Contains("Peso sugerido:"),"Discharged tooltip renders final tonnes and fuel in gallons");
store.Save(Upload(Row(truck2.Name,"EN RUTA",14)));
var historicalUnit=Board().Equipment.Single(x=>x.Code==truck1.Name);
Check(historicalUnit.CompletedTonnes==32.85m && historicalUnit.DispatchedFuelGallons==76.5m,"Partial upload retains figures with the historical load it displays");
var noFiguresHtml=System.Net.WebUtility.HtmlDecode(await RenderChip(new(){Code="MANUAL",Status="finalizado"}));
Check(noFiguresHtml.Contains("Sin registro"),"Manual activation without a trip does not invent tonnage or fuel");
sealed class TempData : ITempDataProvider {public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();public void SaveTempData(HttpContext context,IDictionary<string,object> values){}}
