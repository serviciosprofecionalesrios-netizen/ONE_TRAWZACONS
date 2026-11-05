using System.Reflection;
using System.Security.Claims;
using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
ClaimsPrincipal Person(string email)=>new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Email,email),new Claim(ClaimTypes.Name,email),new Claim(ClaimTypes.Role,"Administrator"),new Claim(UserAccessProfiles.ClaimType,UserAccessProfiles.OperationsHsReadOnly)},"test"));
OperacionesController Controller(ApplicationDbContext db,string email="roldanzamora@trawzacons.com"){
 var http=new DefaultHttpContext{User=Person(email)};
 return new OperacionesController(null!,null!,null!,null!,null!,null!,db,null!,null!,NullLogger<OperacionesController>.Instance,new TestEnvironment(),null!){ControllerContext=new ControllerContext{HttpContext=http},TempData=new TempDataDictionary(http,new TempData())};
}
var options=new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
using(var db=new ApplicationDbContext(options)){
 var controller=Controller(db);
 controller.ActualizarMetaProduccionMensual(2026,10,10500,0,350,0,true);
 Check(controller.TempData.ContainsKey("SeguimientoToneladasMetaOk") && db.OperacionesMetasProduccionMensual.Single().MetaDiariaTritonObjetivo==350 && db.OperacionesMetasProduccionMensual.Single().MetaMensualTriton==10500,"Roldan saves daily 350 and monthly 10500 to persistent storage");
}
using(var db=new ApplicationDbContext(options)){
 var controller=Controller(db);
 var resolve=typeof(OperacionesController).GetMethod("ResolveMetaObjetivo",BindingFlags.Instance|BindingFlags.NonPublic)!;
 decimal Daily(DateTime day){var goal=resolve.Invoke(controller,new object?[]{day,null})!;return (decimal)goal.GetType().GetProperty("MetaDiariaTriton")!.GetValue(goal)!;}
 Check(Daily(new(2026,10,30))==350 && Daily(new(2026,10,31))==350,"Saved daily goal survives a new request and the next day");
 Check(Daily(new(2026,11,1))==350 && Daily(new(2027,1,1))==350,"Latest configured goal carries forward across month and year without resetting to 500");
 Check(Daily(new(2026,9,1))==500,"Future configuration does not rewrite earlier historical months");
 controller.ActualizarMetaProduccionMensual(2026,11,12000,0,400,0,true);
 Check(Daily(new(2026,11,5))==400 && Daily(new(2026,10,31))==350,"Explicit new-month change overrides inherited goal and preserves past month");
 Check(Controller(db,"readonly@example.com").ActualizarMetaProduccionMensual(2026,11,12000,0,999,0,true) is ForbidResult,"Read-only account cannot change production goals");
}
var httpContext=new DefaultHttpContext{User=Person("roldanzamora@trawzacons.com")};httpContext.Request.Method="POST";
var route=new RouteData();route.Values["controller"]="Operaciones";
var authorization=new AuthorizationFilterContext(new ActionContext(httpContext,route,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"action","ActualizarMetaProduccionMensual"}}}),[]);
new AccessProfileAuthorizationFilter().OnAuthorization(authorization);
Check(authorization.Result==null,"Global authorization filter permits Roldan's goal update action");
using(var db=new FailingContext(new DbContextOptionsBuilder<FailingContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)){
 var controller=Controller(db);controller.ActualizarMetaProduccionMensual(2026,11,10500,0,350,0,true);
 Check(controller.TempData.ContainsKey("SeguimientoToneladasMetaError") && !controller.TempData.ContainsKey("SeguimientoToneladasMetaOk"),"Database save failure is reported instead of claiming success with an ephemeral local file");
}
sealed class FailingContext(DbContextOptions<FailingContext> options):ApplicationDbContext(options){public override int SaveChanges()=>throw new InvalidOperationException("Simulated storage failure");}
sealed class TempData:ITempDataProvider{public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>();public void SaveTempData(HttpContext c,IDictionary<string,object> data){}}
sealed class TestEnvironment:IWebHostEnvironment{
 public string ContentRootPath{get;set;}=Path.Combine(Path.GetTempPath(),"goal-check-"+Guid.NewGuid());
 public string WebRootPath{get;set;}="";public string ApplicationName{get;set;}="checks";public string EnvironmentName{get;set;}="Testing";
 public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
}
