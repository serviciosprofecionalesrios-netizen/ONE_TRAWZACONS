using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Security;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.DataProtection;
void Check(bool pass,string name) { if(!pass) throw new Exception(name); Console.WriteLine("PASS: "+name); }
using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
var root=Path.Combine(Path.GetTempPath(),"document-master-check-"+Guid.NewGuid());
var env=new TestEnvironment {ContentRootPath=root};
OperacionesController Controller() {
 var http=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Email,"leslieantonioespinoza@trawzacons.com"),new Claim(ClaimTypes.Role,"Administrator")},"test"))};
 return new OperacionesController(null!,null!,new OperacionesIngresoPersonalTraceStore(db),new OperacionesIngresoPersonalRequestStore(db),new OperacionesIngresoEquipoGondolaTraceStore(db),new OperacionesIngresoEquipoGondolaRequestStore(db),db,null!,null!,NullLogger<OperacionesController>.Instance,env,null!) {
  ControllerContext=new ControllerContext{HttpContext=http}, TempData=new TempDataDictionary(http,new TestTempDataProvider())
 };
}
IFormCollection Form(params (string,string)[] fields)=>new FormCollection(fields.ToDictionary(x=>x.Item1,x=>new StringValues(x.Item2)));
ControlDocumentosViewModel Documents(bool includeInactive=false)=> (ControlDocumentosViewModel)typeof(OperacionesController).GetMethod("BuildControlDocumentosModel",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(Controller(),new object[]{includeInactive})!;
OperationalMasterCatalog.Seed(db);
var driver=db.OperationalMasterRecords.First(x=>x.Kind=="conductores");
var truck=db.OperationalMasterRecords.First(x=>x.Kind=="equipos");
var trailer=db.OperationalMasterRecords.First(x=>x.Kind=="gondolas");
var initial=Documents();
Check(initial.TotalConductores==33 && initial.TotalEquiposGondolas==78,"All 33 drivers, 42 trucks and 36 trailers appear without creating documents");
Check(initial.Filas.All(x=>x.Capacitaciones.All(c=>!c.TieneFecha)),"Missing dates are pending, never fabricated");
Check(initial.CumplimientoCapacitaciones.All(x=>x.CumplimientoPorcentaje==0m),"Missing all dates gives zero compliance instead of 100 percent");
Check(!db.OperacionesIngresoPersonalSolicitudes.Any()&&!db.OperacionesIngresoEquipoGondolaSolicitudes.Any(),"Opening dashboard has no document writes");
var open=Controller();Check(open.NuevoIngresoPersonal(driver.Id) is ViewResult,"Driver master opens personal form");
var personal=(DocumentMasterFormViewModel)open.ViewData["DocumentMasterForm"]!;
Check(personal.Payload["nombres_apellidos"][0]==driver.Name && personal.DisplayId==driver.DisplayId && personal.MasterId==driver.Id,"Full name and TW ID prefilled");
open=Controller();open.NuevoIngresoEquipoGondola(truck.Id);var equipment=(DocumentMasterFormViewModel)open.ViewData["DocumentMasterForm"]!;
Check(equipment.Payload["equipo_codigo_cabezal"][0]==truck.Name && equipment.Payload["equipo_placa_cabezal"][0]==truck.Plate && equipment.Payload["equipo_propietario"][0]==truck.Owner,"Truck code, plate and owner prefilled");
open=Controller();open.NuevoIngresoEquipoGondola(trailer.Id);var gondola=(DocumentMasterFormViewModel)open.ViewData["DocumentMasterForm"]!;
Check(gondola.Type=="Ingreso de Gondola" && gondola.Payload["gondola_placa"][0]==trailer.Name,"Trailer form and plate prefilled");
var equipmentOnly=Controller();equipmentOnly.NuevoIngresoEquipoGondola(kind:"equipos");
Check(((IReadOnlyList<OperationalMasterRecord>)equipmentOnly.ViewData["DocumentMasterChoices"]!).Count==42 && ((IReadOnlyList<OperationalMasterRecord>)equipmentOnly.ViewData["DocumentMasterChoices"]!).All(x=>x.Kind=="equipos"),"Equipment form lists equipment only");
var trailerOnly=Controller();trailerOnly.NuevoIngresoEquipoGondola(kind:"gondolas");
Check(((IReadOnlyList<OperationalMasterRecord>)trailerOnly.ViewData["DocumentMasterChoices"]!).Count==36 && ((IReadOnlyList<OperationalMasterRecord>)trailerOnly.ViewData["DocumentMasterChoices"]!).All(x=>x.Kind=="gondolas"),"Trailer form lists trailers only");
Check(Controller().NuevoIngresoEquipoGondola(truck.Id,kind:"gondolas") is NotFoundResult && Controller().NuevoIngresoEquipoGondola(trailer.Id,kind:"equipos") is NotFoundResult,"Separate forms reject an ID of the other category");
Check(Controller().NuevoIngresoEquipoGondola(kind:"invalid") is NotFoundResult,"Unknown document category rejected");
Check(Controller().NuevoIngresoPersonal(truck.Id) is NotFoundResult,"Equipment ID rejected in personal form");
var created=Controller();created.NuevoIngresoPersonal(Form(("master_record_id",driver.Id.ToString()),("tipo","1. Solicitud Ingreso"),("asunto",driver.Name),("nombres_apellidos","Nombre alterado"),("fecha_inicio","2026-10-01"),("numero_inss","001234"),("licencia","ABC")));
Check(db.OperacionesIngresoPersonalSolicitudes.Count()==1,"Personal documentary form persists to database");
var saved=db.OperacionesIngresoPersonalSolicitudes.Single();var payload=JsonSerializer.Deserialize<Dictionary<string,string[]>>(saved.PayloadJson)!;
Check(saved.SolicitanteNombre==driver.Name && payload["master_record_id"][0]==driver.Id.ToString(),"Server keeps master identity authoritative");
created=Controller();created.NuevoIngresoPersonal(Form(("master_record_id",driver.Id.ToString()),("tipo","1. Solicitud Ingreso"),("asunto",driver.Name),("fecha_inicio","2026-10-02")));
Check(db.OperacionesIngresoPersonalSolicitudes.Count()==1 && db.OperacionesIngresoPersonalSolicitudes.Single().Id==saved.Id,"Repeated save updates same personal document");
payload=JsonSerializer.Deserialize<Dictionary<string,string[]>>(saved.PayloadJson)!;
Check(payload["numero_inss"][0]=="001234" && payload["licencia"][0]=="ABC","Editing preserves fields not submitted by hidden sections");
var after=Documents();Check(after.TotalConductores==33 && after.Filas.Single(x=>x.MasterId==driver.Id).Capacitaciones.Any(c=>c.TieneFecha),"Saved personal dates appear in dashboard without duplicate rows");
Check(after.CumplimientoCapacitaciones.All(x=>x.CumplimientoPorcentaje==Math.Round(100m*(x.Vigentes+x.PorVencer)/33m,1,MidpointRounding.AwayFromZero)),"Compliance includes missing documents in denominator");
var hsUpdate=Controller();hsUpdate.NuevoIngresoPersonal(Form(("master_record_id",driver.Id.ToString()),("tipo","1.3 H&S"),("asunto",driver.Name),("induccion_general_fecha","2026-10-20")));
Check(db.OperacionesIngresoPersonalSolicitudes.Count()==1 && Documents().Filas.Single(x=>x.MasterId==driver.Id).Capacitaciones.Any(x=>x.NombreCapacitacion=="Inducción General"&&x.FechaIso=="2026-10-20"),"Captured H&S training dates become visible in documentary dashboard");
var other=db.OperationalMasterRecords.First(x=>x.Kind=="conductores"&&x.Id!=driver.Id);
Check(Controller().NuevoIngresoPersonal(other.Id,saved.Id) is NotFoundResult,"GET cannot attach another person's document");
var wrong=Controller();wrong.NuevoIngresoPersonal(Form(("master_record_id",other.Id.ToString()),("solicitud_id",saved.Id.ToString()),("tipo","1. Solicitud Ingreso"),("asunto",other.Name)));
Check(wrong.TempData.ContainsKey("NuevoIngresoPersonalError")&&db.OperacionesIngresoPersonalSolicitudes.Count()==1,"POST rejects another person's master ID");
created=Controller();created.NuevoIngresoEquipoGondola(Form(("master_record_id",truck.Id.ToString()),("tipo_ingreso","Ingreso de Equipo"),("asunto",truck.Name),("equipo_codigo_cabezal","Falso"),("equipo_placa_cabezal","Falsa"),("equipo_vencimiento_seguro_vehicular","2027-01-15")));
Check(db.OperacionesIngresoEquipoGondolaSolicitudes.Count()==1,"Truck document persisted");
var savedTruck=db.OperacionesIngresoEquipoGondolaSolicitudes.Single();var truckPayload=JsonSerializer.Deserialize<Dictionary<string,string[]>>(savedTruck.PayloadJson)!;
Check(truckPayload["equipo_codigo_cabezal"][0]==truck.Name && truckPayload["equipo_placa_cabezal"][0]==truck.Plate,"Server prevents changing master equipment identifiers");
created=Controller();created.NuevoIngresoEquipoGondola(Form(("master_record_id",truck.Id.ToString()),("tipo_ingreso","Ingreso de Equipo"),("asunto",truck.Name),("equipo_vencimiento_seguro_vehicular","2027-03-15")));
Check(db.OperacionesIngresoEquipoGondolaSolicitudes.Count()==1 && Documents().EquiposGondolas.Single(x=>x.MasterId==truck.Id).VencimientoSeguroVehicular=="15/03/2027","Truck dates update same document and dashboard");
created=Controller();created.NuevoIngresoEquipoGondola(Form(("master_record_id",trailer.Id.ToString()),("tipo_ingreso","Ingreso de Gondola"),("asunto",trailer.Name),("gondola_fecha_inicio","2026-10-21")));
Check(db.OperacionesIngresoEquipoGondolaSolicitudes.Count()==2 && Documents().TotalEquiposGondolas==78,"Trailer completion does not duplicate catalog");
// An old document without a master ID is matched by exact identifier and preserved.
var oldTruck=db.OperationalMasterRecords.First(x=>x.Kind=="equipos"&&x.Id!=truck.Id);
db.OperacionesIngresoEquipoGondolaSolicitudes.Add(new(){Tipo="Ingreso de Equipo",Asunto=oldTruck.Name,PayloadJson=JsonSerializer.Serialize(new Dictionary<string,string[]>{{"equipo_codigo_cabezal",[oldTruck.Name]},{"equipo_vencimiento_emision_gases",["2027-02-01"]}}),UpdatedAt=DateTime.Now});db.SaveChanges();
Check(Documents().TotalEquiposGondolas==78 && Documents().EquiposGondolas.Single(x=>x.MasterId==oldTruck.Id).VencimientoEmisionGases=="01/02/2027","Existing legacy documentary dates preserved without duplicates");
var quickEdit=Controller();quickEdit.ActualizarControlDocumentos(new ControlDocumentosUpdateInput {Conductor=driver.Name,Encabezados=new(){"Licencia"},Fechas=new(){"2026-10-20"},SupervisorNombre="Prueba",ConfirmacionSupervisor=true});
Check(quickEdit.TempData.ContainsKey("ControlDocumentosOk") && Documents().Filas.Single(x=>x.MasterId==driver.Id).Capacitaciones.Single(x=>x.NombreCapacitacion=="Licencia").FechaIso=="2026-10-20","Inline documentary edits are reflected in dashboard");
driver.Name="Nombre Completo Corregido Prueba";driver.RecordKey=OperationalMasterCatalog.Key(driver.Kind,driver.Name);db.SaveChanges();
Check(Documents().Filas.Single(x=>x.MasterId==driver.Id).RequestId==saved.Id,"Driver rename keeps documentary association by immutable ID");
Check(Documents().Filas.Single(x=>x.MasterId==driver.Id).Capacitaciones.Single(x=>x.NombreCapacitacion=="Licencia").FechaIso=="2026-10-20","Driver rename retains existing inline date overrides");
var inactive=db.OperationalMasterRecords.First(x=>x.Kind=="conductores"&&x.Id!=driver.Id);inactive.IsActive=false;db.SaveChanges();
Check(Controller().NuevoIngresoPersonal(inactive.Id) is NotFoundResult,"Inactive driver cannot start new document");
Check(Documents().TotalConductores==32,"Inactive undocumented driver excluded");
// Import aliases are explicitly linked to the existing master's stable source key.
var importDirectory=Path.Combine(root,"Data","Operaciones");Directory.CreateDirectory(importDirectory);
var importedTarget=db.OperationalMasterRecords.First(x=>x.Kind=="conductores" && x.IsActive && x.Id!=driver.Id);
File.WriteAllText(Path.Combine(importDirectory,"ControlDocumentosPersonas.json"),JsonSerializer.Serialize(new[]{
 new {Nombre="Nombre alternativo confirmado",SourceKey=driver.SourceKey,MasterId=(int?)null,Fechas=new Dictionary<string,string>{{"IPER","01/09/2026"}},Datos=new Dictionary<string,string>{{"Cargo","Operador"}}},
 new {Nombre=driver.Name,SourceKey=(string?)null,MasterId=(int?)importedTarget.Id,Fechas=new Dictionary<string,string>{{"Prevención de Incendios","02/09/2026"}},Datos=new Dictionary<string,string>()}
}));
var linked=Documents();
Check(linked.TotalConductores==32,"Confirmed imported alias joins existing TW driver without adding a duplicate");
Check(linked.Filas.Single(x=>x.MasterId==driver.Id).Capacitaciones.Single(x=>x.NombreCapacitacion=="IPER").FechaIso=="2026-09-01","Imported training retained on master driver");
Check(linked.Filas.Single(x=>x.MasterId==driver.Id).Datos["Cargo"]=="Operador","Imported profile retained after merging with existing document");
Check(linked.Filas.Single(x=>x.MasterId==importedTarget.Id).Capacitaciones.Single(x=>x.NombreCapacitacion=="Prevención de Incendios").FechaIso=="2026-09-02" && !linked.Filas.Single(x=>x.MasterId==driver.Id).Capacitaciones.Single(x=>x.NombreCapacitacion=="Prevención de Incendios").TieneFecha,"Explicit master ID wins over a conflicting name without mixing people's training");
var catalog=db.OperationalMasterRecords.AsNoTracking().ToList();
Check(OperationalMasterIdentity.Resolve(catalog,"conductores",driver.Id,other.Name)?.Id==driver.Id,"Master ID remains authoritative after a name change");
Check(OperationalMasterIdentity.Resolve(catalog,"conductores",truck.Id,driver.Name)==null,"ID from another category cannot fall back to matching name");
Check(OperationalMasterIdentity.Resolve(catalog,"conductores",null,"Unrelated Person")==null,"Unconfirmed name similarity never links a person");
var assetCatalog=new[]{new OperationalMasterRecord{Id=9001,Kind="equipos",Name="CAB-01",Plate="M 123-456"},new OperationalMasterRecord{Id=9002,Kind="equipos",Name="CAB-02",Plate="M 654-321"},new OperationalMasterRecord{Id=9003,Kind="gondolas",Name="M 123-456"}};
Check(OperationalMasterIdentity.Resolve(assetCatalog,"equipos",null,"", "m123456")?.Id==9001,"Truck plate formatting differences resolve to one existing ID");
Check(OperationalMasterIdentity.Resolve(assetCatalog,"gondolas",null,"m123456")?.Id==9003,"Trailer plate formatting resolves within its own category");
Check(OperationalMasterIdentity.Resolve(assetCatalog,"equipos",null,"CAB-01","M654321")==null,"Conflicting truck code and plate remain unlinked for review");
var duplicatePlate=assetCatalog.Append(new OperationalMasterRecord{Id=9004,Kind="equipos",Name="CAB-03",Plate="M123456"});
Check(OperationalMasterIdentity.Resolve(duplicatePlate,"equipos",null,"","M123456")==null,"Ambiguous duplicate plates are never assigned to multiple masters");
db.OperacionesIngresoEquipoGondolaSolicitudes.Add(new(){Tipo="Ingreso de Equipo",Asunto="Documento importado por placa",PayloadJson=JsonSerializer.Serialize(new Dictionary<string,string[]>{{"equipo_placa_cabezal",[truck.Plate!.Replace("-", "").ToLowerInvariant()]},{"equipo_vencimiento_emision_gases",["2027-04-01"]}}),UpdatedAt=DateTime.Now.AddMinutes(1)});db.SaveChanges();
var mergedAssets=Documents();
Check(mergedAssets.TotalEquiposGondolas==78 && mergedAssets.EquiposGondolas.Single(x=>x.MasterId==truck.Id).VencimientoEmisionGases=="01/04/2027" && mergedAssets.EquiposGondolas.Single(x=>x.MasterId==truck.Id).VencimientoSeguroVehicular=="15/03/2027","Repeated truck imports resolve to same TW ID and retain complementary dates");
foreach(var entry in new[]{("leslieantonioespinoza@trawzacons.com","SupervisorHS",true),("hs@example.com","GestorHS",true),("manager@example.com","GerenteHS",true),("supervisor@example.com","SupervisorHS",true),("roldanzamora@trawzacons.com","Monitoreo",false),("yerlinrocha@trawzacons.com","SupervisorMLL",false)}) {
 var user=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Email,entry.Item1),new Claim(ClaimTypes.Role,"EndUser"),new Claim(UserAccessProfiles.BusinessRoleClaimType,entry.Item2)},"test"));
 foreach(var action in new[]{"NuevoIngresoPersonal","NuevoIngresoEquipoGondola","ActualizarControlDocumentos","ActualizarControlDocumentosMasivo"}) {
  var http=new DefaultHttpContext {User=user};http.Request.Method="POST";
  var routes=new RouteData();routes.Values["controller"]="Operaciones";
  var ctx=new AuthorizationFilterContext(new ActionContext(http,routes,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"action",action}}}),new List<IFilterMetadata>());
  new AccessProfileAuthorizationFilter().OnAuthorization(ctx);
  Check((ctx.Result==null)==entry.Item3,$"Document save permissions: {entry.Item1} {action}");
 }
 if(entry.Item3) {
  var http=new DefaultHttpContext{User=user};http.Request.Method="POST";var routes=new RouteData();routes.Values["controller"]="Operaciones";
  var ctx=new AuthorizationFilterContext(new ActionContext(http,routes,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"action","MaestroImportar"}}}),new List<IFilterMetadata>());
  new AccessProfileAuthorizationFilter().OnAuthorization(ctx);Check(ctx.Result is ForbidResult,"HS cannot modify operational masters");
 }
}
var stateMethod=typeof(OperacionesController).GetMethod("BuildControlDocumentoCapacitacionEstado",BindingFlags.NonPublic|BindingFlags.Static)!;
ControlDocumentosCapacitacionEstadoViewModel Training(string header,string date)=>(ControlDocumentosCapacitacionEstadoViewModel)stateMethod.Invoke(null,new object[]{header,date})!;
foreach(var header in new[]{"Inducción General de Sitio","Inducción General","Primeros Auxilios Teórico","Primeros Auxilios Práctico"})
 Check(Training(header,"01/03/2023").FechaVencimiento=="01/03/2026","Three calendar years across leap year: "+header);
Check(Training("Primeros Auxilios Práctico","29/02/2024").FechaVencimiento=="28/02/2027","Leap-day training expires on the third anniversary");
foreach(var header in new[]{"NCS","Uso de Formato de Transporte","Educación Vial","Oper. Traslado Mina a  Mina","Operación de Traslado Mina a Mina","Gestión de Fatiga"})
 Check(Training(header,"01/03/2023").FechaVencimiento=="01/03/2025","Two calendar years across leap year: "+header);
bool Restricted(string header)=>header is "ATS" or "Manejo de Equipos Livianos" or "Manejo Defensivo (V. Liviano)" or "Manejo Defensivo Liviano";
foreach(var header in new[]{"IPER","Manejo de Residuos Sólidos","Manejo de Hidrocarburos","Sistema de Gestión Social","Sistema 5 puntos","Respuesta ante Emergencia","Prevención de Incendios","Manejo Defensivo (V. Pesado)","Otra capacitación","ATS","Manejo de Equipos Livianos","Manejo Defensivo (V. Liviano)"})
 Check(Training(header,"01/03/2023").FechaVencimiento=="01/03/2028","Other training lasts five calendar years: "+header);
Check(Training("IPER","29/02/2024").FechaVencimiento=="28/02/2029","Five-year training handles leap-day anniversaries");
foreach(var header in new[]{"Cedula","INSS","Contrato de Trabajo","Licencia","Examen medico"})
 Check(Training(header,"01/03/2023").FechaVencimiento=="28/02/2025","Personal document retains existing validity: "+header);
Check(!Documents().EncabezadosCapacitacion.Any(Restricted),"No restricted courses appear without a mechanic or supervisor");
var hsPosition=Controller();hsPosition.NuevoIngresoPersonal(Form(("master_record_id",driver.Id.ToString()),("tipo","1. Solicitud Ingreso"),("asunto",driver.Name),("cargo","Mecánico"),("ats_fecha","2026-10-20")));
Check(!hsPosition.TempData.ContainsKey("NuevoIngresoPersonalError"),"Mechanic cargo update saves: "+hsPosition.TempData["NuevoIngresoPersonalError"]);
var scoped=Documents();var mechanic=scoped.Filas.Single(x=>x.MasterId==driver.Id);
Check(mechanic.Datos.GetValueOrDefault("Cargo")=="Mecánico","Saved mechanic position reaches documentary profile: "+mechanic.Datos.GetValueOrDefault("Cargo"));
Check(scoped.EncabezadosCapacitacion.Contains("ATS") && scoped.EncabezadosCapacitacion.Contains("Manejo de Equipos Livianos") && scoped.EncabezadosCapacitacion.Contains("Manejo Defensivo (V. Liviano)"),"Restricted courses appear for a recorded mechanic");
Check(mechanic.Capacitaciones.Where(x=>Restricted(x.NombreCapacitacion)).All(x=>x.Aplica),"Mechanic must satisfy restricted courses");
Check(scoped.Filas.Where(x=>x.MasterId!=driver.Id).All(x=>x.Capacitaciones.Where(c=>Restricted(c.NombreCapacitacion)).All(c=>!c.Aplica && c.EstadoFiltro=="noaplica")),"Drivers with no qualifying position are not marked missing for restricted courses");
Check(scoped.CumplimientoCapacitaciones.Where(x=>Restricted(x.Capacitacion)).All(x=>x.TotalConductores==1),"Restricted course compliance counts only eligible staff");
foreach(var position in new[]{"Supervisor","Conductor"}){
 Controller().NuevoIngresoPersonal(Form(("master_record_id",driver.Id.ToString()),("tipo","1. Solicitud Ingreso"),("asunto",driver.Name),("cargo",position)));
 Check(Documents().EncabezadosCapacitacion.Any(Restricted)==(position=="Supervisor"),"Visibility follows recorded position: "+position);
}
foreach(var kind in new[]{"conductores","equipos","gondolas"}){
 var master=new OperationalMasterRecord{Kind=kind,Name="Document state "+kind,RecordKey="document-state:"+kind,Owner="Test",Tenure="Propio"};db.Add(master);db.SaveChanges();
 var actor=Controller();
 var disabled=await actor.MaestroEstado(master.Id,false,default,controlDocumentos:true,includeInactive:true) as RedirectToActionResult;
 Check(disabled?.ActionName=="ControlDocumentos" && disabled.RouteValues?["includeInactive"] is true,"State change returns to documents and preserves inactive selection: "+kind);
 Check(actor.TempData.ContainsKey("ControlDocumentosOk"),"Status confirmation appears in documents: "+kind);
 Check(!db.OperationalMasterRecords.AsNoTracking().Single(x=>x.Id==master.Id).IsActive,"Document action persists deactivation in shared master: "+kind);
 var all=Documents(true);
 Check(all.IncludeInactive && (kind=="conductores" ? all.Filas.Any(x=>x.MasterId==master.Id && x.IsActive==false) : all.EquiposGondolas.Any(x=>x.MasterId==master.Id && x.IsActive==false)),"Inactive undocumented master can be found for reactivation: "+kind);
 Check(kind=="conductores" ? !Documents().Filas.Any(x=>x.MasterId==master.Id) : !Documents().EquiposGondolas.Any(x=>x.MasterId==master.Id),"Default view retains existing active-only behavior for undocumented masters: "+kind);
 await Controller().MaestroEstado(master.Id,true,default,controlDocumentos:true);
 Check(db.OperationalMasterRecords.AsNoTracking().Single(x=>x.Id==master.Id).IsActive,"Document action reactivates original master ID: "+kind);
 var forbidden=Controller();forbidden.HttpContext.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Email,"hs@example.com"),new Claim(ClaimTypes.Role,"GestorHS")},"test"));
 Check(await forbidden.MaestroEstado(master.Id,false,default,controlDocumentos:true) is ForbidResult && master.IsActive,"Other HS users cannot bypass shared activation permissions: "+kind);
}
var documentedId=driver.Id;var requestCount=db.OperacionesIngresoPersonalSolicitudes.Count();
await Controller().MaestroEstado(documentedId,false,default,controlDocumentos:true);
Check(Documents().Filas.Single(x=>x.MasterId==documentedId).IsActive==false && db.OperacionesIngresoPersonalSolicitudes.Count()==requestCount,"Deactivating documented driver keeps history and marks inactive");
await Controller().MaestroEstado(documentedId,true,default,controlDocumentos:true);
var renderBuilder=Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions{ApplicationName=typeof(OperacionesController).Assembly.FullName});
renderBuilder.Services.AddControllersWithViews().AddApplicationPart(typeof(OperacionesController).Assembly);
renderBuilder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
await using var renderApp=renderBuilder.Build();using var renderScope=renderApp.Services.CreateScope();
var razor=renderScope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Mvc.Razor.IRazorViewEngine>();
async Task<string> StateButtons(int? masterId,bool? active,string email){
 var http=new DefaultHttpContext{RequestServices=renderScope.ServiceProvider,User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Email,email),new Claim(ClaimTypes.Role,"GestorHS")},"test"))};
 var routes=new RouteData();routes.Routers.Add(new RouteCollection());routes.Values["controller"]="Operaciones";
 var action=new ActionContext(http,routes,new ActionDescriptor());var view=razor.GetView(null,"/Views/Operaciones/_DocumentMasterState.cshtml",false);
 Check(view.Success,"Actual document activation controls resolve");using var writer=new StringWriter();
 var data=new ViewDataDictionary<(int? Id,bool? IsActive,bool IncludeInactive)>(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary()){Model=(masterId,active,true)};
 await view.View!.RenderAsync(new ViewContext(action,view.View,data,new TempDataDictionary(http,new TestTempDataProvider()),writer,new HtmlHelperOptions()));return writer.ToString();
}
var activeHtml=await StateButtons(driver.Id,true,"leslieantonioespinoza@trawzacons.com");
Check(activeHtml.Contains("Desactivar") && activeHtml.Contains("__RequestVerificationToken") && activeHtml.Contains("controlDocumentos"),"Leslie sees a protected deactivate form returning to documents");
Check((await StateButtons(driver.Id,false,"leslieantonioespinoza@trawzacons.com")).Contains("Activar"),"Inactive master renders activation button");
Check(!(await StateButtons(driver.Id,true,"hs@example.com")).Contains("<form"),"HS user without shared permission sees no activation form");
Check(!(await StateButtons(null,null,"leslieantonioespinoza@trawzacons.com")).Contains("<form"),"Unlinked historical document cannot change another master");
// Public document views must partition existing history without changing master states.
var legacyName="Histórico sin vínculo prueba";
var importedPath=Path.Combine(root,"Data","Operaciones","ControlDocumentosPersonas.json");
var importedRows=JsonSerializer.Deserialize<List<Dictionary<string,object>>>(File.ReadAllText(importedPath))!;
importedRows.Add(new(){["Nombre"]=legacyName,["Datos"]=new Dictionary<string,string>{{"Solicitud de origen","csv-fixture"}},["Fechas"]=new Dictionary<string,string>{{"IPER","20/10/2026"}},["CreatedAt"]="2026-10-20T00:00:00",["UpdatedAt"]="2026-10-20T00:00:00"});
File.WriteAllText(importedPath,JsonSerializer.Serialize(importedRows));
var newName="Ingreso nuevo sin catálogo prueba";
db.OperacionesIngresoPersonalSolicitudes.Add(new(){Tipo="Control documentos H&S - Persona",Asunto=newName,SolicitanteNombre=newName,PayloadJson=JsonSerializer.Serialize(new Dictionary<string,string[]>{{"nombres_apellidos",new[]{newName}}}),CreatedAt=DateTime.Now,UpdatedAt=DateTime.Now});
var inactiveMaster=new OperationalMasterRecord{Kind="conductores",Name="Inactivo sin documentos prueba",RecordKey="inactive-group-fixture",IsActive=false};
db.Add(inactiveMaster);db.SaveChanges();
var masterStates=db.OperationalMasterRecords.AsNoTracking().ToDictionary(x=>x.Id,x=>x.IsActive);
var principal=(ControlDocumentosViewModel)((ViewResult)await Controller().ControlDocumentos()).Model!;
var archived=(ControlDocumentosViewModel)((ViewResult)await Controller().ControlDocumentos(driverView:"desactivados")).Model!;
Check(!principal.Filas.Any(x=>x.Conductor==legacyName)&&archived.Filas.Any(x=>x.Conductor==legacyName),"Histórico sin vínculo se consulta sólo en conductores desactivados");
Check(principal.Filas.Any(x=>x.Conductor==newName&&x.IsNewAdmission)&&!archived.Filas.Any(x=>x.Conductor==newName),"Nuevo ingreso sin antecedentes permanece en la vista principal");
Check(archived.Filas.Any(x=>x.MasterId==inactiveMaster.Id)&&!principal.Filas.Any(x=>x.MasterId==inactiveMaster.Id),"Maestro inactivo sin documentos también aparece en desactivados");
Check(principal.Filas.All(x=>!x.IsInDeactivatedView)&&archived.Filas.All(x=>x.IsInDeactivatedView),"Las dos vistas son disjuntas y sus KPI usan sólo sus filas");
Check(principal.MainDriverCount==principal.Filas.Count&&principal.DeactivatedDriverCount==archived.Filas.Count,"Contadores de navegación coinciden con los listados");
Check(db.OperationalMasterRecords.AsNoTracking().All(x=>x.IsActive==masterStates[x.Id]),"Clasificar registros no modifica estados reales");
var linkedMaster=new OperationalMasterRecord{Kind="conductores",Name=legacyName,RecordKey=OperationalMasterCatalog.Key("conductores",legacyName),IsActive=true};db.Add(linkedMaster);db.SaveChanges();
var linkedView=(ControlDocumentosViewModel)((ViewResult)await Controller().ControlDocumentos()).Model!;
var afterLinkArchive=(ControlDocumentosViewModel)((ViewResult)await Controller().ControlDocumentos(driverView:"desactivados")).Model!;
var linkedRow=linkedView.Filas.Single(x=>x.MasterId==linkedMaster.Id);
Check(linkedRow.Capacitaciones.Single(x=>x.NombreCapacitacion=="IPER").FechaIso=="2026-10-20"&&!afterLinkArchive.Filas.Any(x=>x.Conductor==legacyName),"Alta en maestro vincula automáticamente el histórico conservando sus fechas");
Check(!linkedRow.IsNewAdmission,"Histórico vinculado conserva su procedencia y no se etiqueta como nuevo");
Console.WriteLine("PASS: master-document integration, training rules, activation and driver view groups.");
sealed class TestTempDataProvider : ITempDataProvider { public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();public void SaveTempData(HttpContext context,IDictionary<string,object> values){} }
sealed class TestEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment {
 public string ContentRootPath {get;set;}="";public string WebRootPath {get;set;}="";public string EnvironmentName{get;set;}="Testing";public string ApplicationName{get;set;}="Check";
 public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider{get;set;}=new Microsoft.Extensions.FileProviders.NullFileProvider();
 public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider{get;set;}=new Microsoft.Extensions.FileProviders.NullFileProvider();
}
