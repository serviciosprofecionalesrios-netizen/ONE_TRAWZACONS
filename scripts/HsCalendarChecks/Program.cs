using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Operaciones;
void Check(bool pass,string message){if(!pass)throw new Exception(message);Console.WriteLine("PASS: "+message);}
var today=new DateTime(2026,11,20);
ControlDocumentosCapacitacionEstadoViewModel Cap(string name,int days)=>new(){NombreCapacitacion=name,Fecha="20/12/2021",FechaVencimiento=today.AddDays(days).ToString("dd/MM/yyyy",System.Globalization.CultureInfo.InvariantCulture)};
var active=new ControlDocumentosRowViewModel {MasterId=1,MasterCode="TEST-1",IsActive=true,Conductor="Fixture activo",Capacitaciones=new[]{Cap("31 días",31),Cap("30 días",30),Cap("16 días",16),Cap("15 días",15),Cap("1 día",1),Cap("hoy",0),Cap("anterior",-400),Cap("30 días",30),Cap("Cédula",30),new(){NombreCapacitacion="Sin fecha"},new(){NombreCapacitacion="No aplica",Aplica=false,FechaVencimiento="20/12/2026"}}};
var documents=new ControlDocumentosViewModel{Filas=new[]{active,new ControlDocumentosRowViewModel{MasterId=2,IsActive=false,Capacitaciones=new[]{Cap("Inactivo",1)}},new ControlDocumentosRowViewModel{IsActive=null,Capacitaciones=new[]{Cap("Sin vínculo",1)}}}};
var model=HsCalendarSource.Project(documents,today);
Check(model.ActiveDrivers==1,"Excluye inactivos y personas sin estado activo confirmado");
Check(model.Valid==1 && model.Due==4 && model.Expired==2,"KPI por registro único, sin documentos personales ni fechas ausentes");
Check(model.Events.Count==8 && model.Alerts.Count==6,"Un evento por vencimiento; alertas sin duplicados ni documentos personales");
Check(model.Events.Single(x=>x.Title=="30 días").Date==new DateTime(2026,12,20),"Alerta desde 20/11; único evento el 20/12");
foreach(var (days,tone,state) in new[]{(31,"success","vigente"),(30,"warning","porvencer"),(16,"warning","porvencer"),(15,"orange","porvencer"),(1,"orange","porvencer"),(0,"danger","vencida"),(-1,"danger","vencida")})Check(HsCalendarSource.Tone(days)==tone&&HsCalendarSource.State(days)==state,$"Umbral {days}");
Check(model.Events.Where(x=>x.Days<=30).All(x=>x.RedIndicator),"Indicador rojo en todas las alertas");
Check(HsCalendarSource.Local(new DateTime(2026,11,21,5,59,0,DateTimeKind.Utc)).Date==today,"Medianoche Nicaragua independiente del servidor");
Check(HsCalendarSource.Project(documents,today.AddDays(31)).Alerts.Count==7,"Vencida persiste en alertas después de la fecha");
active.Capacitaciones=new[]{Cap("Renovada",365)};
Check(HsCalendarSource.Project(documents,today).Alerts.Count==0,"Renovar o eliminar retira las alertas anteriores");
active.IsActive=false;
Check(HsCalendarSource.Project(documents,today).ActiveDrivers==0,"Desactivar retira conductor y conteos");
Check(HsCalendarSource.Project(new(),today).Events.Count==0,"Fuente vacía no genera datos ficticios");
var inbox=new HsAlertInbox();inbox.Replace(model.Alerts.Concat(model.Alerts));
Check(inbox.Alerts.Count==6,"Bandeja atómica no acumula duplicados");inbox.Replace(Array.Empty<ITServiceDeskApp.ViewModels.Hs.HsOperationalEvent>());
Check(inbox.Alerts.Count==0,"Evaluación sin pendientes limpia la bandeja");
var builder=Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions{ApplicationName=typeof(ITServiceDeskApp.Controllers.HsController).Assembly.FullName});
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ITServiceDeskApp.Controllers.HsController).Assembly);
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
await using var app=builder.Build();using var scope=app.Services.CreateScope();
var razor=scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Mvc.Razor.IRazorViewEngine>();
async Task<string> Render(string calendarView,int days){
 var http=new Microsoft.AspNetCore.Http.DefaultHttpContext{RequestServices=scope.ServiceProvider};
 var route=new RouteData();route.Routers.Add(new RouteCollection());route.Values["controller"]="Hs";route.Values["action"]="Calendar";
 var action=new ActionContext(http,route,new ActionDescriptor());
 var view=razor.GetView(null,"/Views/Hs/Calendar.cshtml",false);Check(view.Success,"Razor resuelve calendario "+calendarView);
 var vm=HsCalendarSource.Project(documents,today);vm.Start=new DateTime(2026,11,2);vm.End=vm.Start.AddDays(days);vm.Anchor=today;vm.View=calendarView;
 using var writer=new StringWriter();var data=new ViewDataDictionary<ITServiceDeskApp.ViewModels.Hs.HsOperationalCalendar>(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary()){Model=vm};
 await view.View!.RenderAsync(new Microsoft.AspNetCore.Mvc.Rendering.ViewContext(action,view.View,data,new TempDataDictionary(http,new MemoryTempData()),writer,new HtmlHelperOptions()));return writer.ToString();
}
var week=await Render("week",7);var month=await Render("month",35);
Check(System.Text.RegularExpressions.Regex.Matches(week,"class=\"hs-day\"").Count==7,"Vista semanal renderiza siete días");
Check(System.Text.RegularExpressions.Regex.Matches(week,"class=\"hs-hour\"").Count==168,"Semana renderiza 24 horas por día");
Check(System.Text.RegularExpressions.Regex.Matches(month,"class=\"hs-day\"").Count==35&&!month.Contains("class=\"hs-hour\""),"Mes compacto renderiza todas las fechas");
Check(week.Contains("hsDayDrawer")&&week.Contains("hsEvents"),"Drawer y datos de eventos presentes en Razor");
sealed class MemoryTempData:ITempDataProvider{
 public IDictionary<string,object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context)=>new Dictionary<string,object>();
 public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context,IDictionary<string,object> values){}
}
