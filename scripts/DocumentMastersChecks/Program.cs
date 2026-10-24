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
ControlDocumentosViewModel Documents()=> (ControlDocumentosViewModel)typeof(OperacionesController).GetMethod("BuildControlDocumentosModel",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(Controller(),null)!;
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
 new {Nombre=driver.Name,SourceKey=(string?)null,MasterId=(int?)importedTarget.Id,Fechas=new Dictionary<string,string>{{"ATS","02/09/2026"}},Datos=new Dictionary<string,string>()}
}));
var linked=Documents();
Check(linked.TotalConductores==32,"Confirmed imported alias joins existing TW driver without adding a duplicate");
Check(linked.Filas.Single(x=>x.MasterId==driver.Id).Capacitaciones.Single(x=>x.NombreCapacitacion=="IPER").FechaIso=="2026-09-01","Imported training retained on master driver");
Check(linked.Filas.Single(x=>x.MasterId==driver.Id).Datos["Cargo"]=="Operador","Imported profile retained after merging with existing document");
Check(linked.Filas.Single(x=>x.MasterId==importedTarget.Id).Capacitaciones.Single(x=>x.NombreCapacitacion=="ATS").FechaIso=="2026-09-02" && !linked.Filas.Single(x=>x.MasterId==driver.Id).Capacitaciones.Single(x=>x.NombreCapacitacion=="ATS").TieneFecha,"Explicit master ID wins over a conflicting name without mixing people's training");
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
Console.WriteLine("PASS: master-document integration, real database stores (isolated InMemory), defaults, persistence, stable IDs, duplicate prevention, legacy records and H&S permissions.");
sealed class TestTempDataProvider : ITempDataProvider { public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();public void SaveTempData(HttpContext context,IDictionary<string,object> values){} }
sealed class TestEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment {
 public string ContentRootPath {get;set;}="";public string WebRootPath {get;set;}="";public string EnvironmentName{get;set;}="Testing";public string ApplicationName{get;set;}="Check";
 public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider{get;set;}=new Microsoft.Extensions.FileProviders.NullFileProvider();
 public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider{get;set;}=new Microsoft.Extensions.FileProviders.NullFileProvider();
}
