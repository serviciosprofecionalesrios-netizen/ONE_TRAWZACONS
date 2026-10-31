using System.Reflection;
using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Operaciones;
using OfficeOpenXml;
using ITServiceDeskApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
var day = new DateTime(2026, 10, 24);
SeguimientoToneladasRowViewModel Row(string unit, int offset, string state, decimal tons = 0) => new() { Equipo=unit, FechaEvento=day.AddDays(offset), Estado=state, Toneladas=tons, Procedencia="TRITON", Ruta="TRITON - DESTINO", Viajes=1 };
var sample = new[] {Row("W-0119", -1,"EN RUTA"), Row("W-0105", -1,"EN RUTA"),Row("CLOSED",-2,"EN RUTA"),Row("CLOSED",-1,"FINALIZADO",30),Row("TODAY",0,"FINALIZADO",32),Row("FUTURE",1,"EN RUTA")};
var selected=TonnageStatusRows.Select(sample,day);
Check(selected.Count==3 && selected.Any(x=>x.Equipo=="W-0119") && selected.Any(x=>x.Equipo=="W-0105"),"Previous-day open trips remain visible, closed and future trips do not carry");
Check(selected.Where(x=>x.Estado=="FINALIZADO").Sum(x=>x.Toneladas)==32,"Daily discharged tonnage is unchanged");
Check(TonnageStatusRows.Select(sample.Append(Row("W-0119",0,"FINALIZADO",31)),day).Count(x=>x.Equipo=="W-0119")==1,"Current-day trip replaces prior-day open trip without duplicate");
ExcelPackage.LicenseContext=LicenseContext.NonCommercial;
var parser=typeof(OperacionesController).GetMethod("ParseToneladasRowsFromWorkbook",BindingFlags.Static|BindingFlags.NonPublic)!;
using(var fixture=new ExcelPackage()){
 var sheet=fixture.Workbook.Worksheets.Add("TiHouse");
 string[] headers=["C. Equipo","P. Neto","Fecha","Fecha Salida","Estado","Procedencia","Conductor"];
 for(var c=0;c<headers.Length;c++)sheet.Cells[1,c+1].Value=headers[c];
 var rows=new object?[][]{["W-0073",32.85m,"28/10/2026 | 08:22 PM","29/10/2026 05:03 AM","FINALIZADO","TRITON","Edwin Brenes"],["OPEN",0m,"28/10/2026 | 08:22 PM","29/10/2026 05:03 AM","EN RUTA","TRITON","Conductor"],["NO-EXIT",30m,"28/10/2026 | 08:22 PM",null,"FINALIZADO","TRITON","Conductor"]};
 for(var r=0;r<rows.Length;r++)for(var c=0;c<headers.Length;c++)sheet.Cells[r+2,c+1].Value=rows[r][c];
 object?[] parameters=[fixture.Workbook,null,null];
 var parsed=(List<SeguimientoToneladasRowViewModel>)parser.Invoke(null,parameters)!;
 var corrected=parsed.Single(x=>x.Equipo=="W-0073");
 Check(corrected.FechaEvento==new DateTime(2026,10,29,5,3,0) && corrected.FechaRegistroOrigen==new DateTime(2026,10,28,20,22,0),"Completed TiHouse trip uses actual exit date and retains original registration time");
 Check(parsed.Where(x=>x.Equipo!="W-0073").All(x=>x.FechaEvento==new DateTime(2026,10,28,20,22,0)),"Open trips retain registration date; completed trips with missing exit retain compatible fallback");
 using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
 var store=new OperacionesSeguimientoStore(db);
 var legacy=Row("W-0073",0,"FINALIZADO",32.85m);legacy.FechaEvento=corrected.FechaRegistroOrigen;legacy.Conductor=corrected.Conductor;legacy.Ruta=corrected.Ruta;
 var unrelated=Row("W-0073",0,"FINALIZADO",20m);unrelated.FechaEvento=new DateTime(2026,10,28,12,0,0);
 store.Save(new(){Rows=[legacy,unrelated],IsFromUpload=true});
 store.Save(new(){Rows=[corrected],IsFromUpload=true});
 Check(db.OperacionesSeguimientoRegistros.Count()==2 && db.OperacionesSeguimientoRegistros.Single(x=>x.Toneladas==32.85m).FechaOperativa==new DateTime(2026,10,29),"Reload relocates the precise legacy event instead of counting it on both days");
 Check(db.OperacionesSeguimientoRegistros.Single(x=>x.Toneladas==20m).FechaOperativa==new DateTime(2026,10,28),"Another genuine trip on the previous day is preserved");
 store.Save(new(){Rows=[corrected],IsFromUpload=true});
 Check(db.OperacionesSeguimientoRegistros.Count()==2,"Reimport remains idempotent after correcting event date");
}
if(args.Length>0) {
 ExcelPackage.LicenseContext=LicenseContext.NonCommercial;
 using var package=new ExcelPackage(new FileInfo(args[0]));
 var parse=typeof(OperacionesController).GetMethod("ParseToneladasRowsFromWorkbook",BindingFlags.Static|BindingFlags.NonPublic)!;
 object?[] parseArgs=[package.Workbook,null,null];
 var rows=(List<SeguimientoToneladasRowViewModel>)parse.Invoke(null,parseArgs)!;
 var expectedDay=args.Length>1?DateTime.ParseExact(args[1],"yyyy-MM-dd",CultureInfo.InvariantCulture):day;
 Check(rows.Count>0,"Actual TiHouse workbook parsed");
 foreach(var type in new[]{typeof(OperacionesController),typeof(OperacionesSeguimientoStore)}) {
  var build=type.GetMethod("BuildToneladasModel",BindingFlags.Static|BindingFlags.NonPublic)!;
  var model=(SeguimientoToneladasViewModel)build.Invoke(null,[rows])!;
  Check(model.FechaOperativa==model.Rows.Max(x=>x.FechaEvento?.Date),"Operational date follows the latest day in the workbook, independently of the selected historical day");
  var status=TonnageStatusRows.Select(model.Rows,expectedDay);
  if(expectedDay==day)foreach(var unit in new[]{"W-0119","W-0105"}) Check(status.Any(x=>x.Equipo==unit&&x.Estado=="EN RUTA"),type.Name+": "+unit+" remains in route");
  if(expectedDay==new DateTime(2026,10,29)) Check(status.Any(x=>x.Equipo=="W-0073" && x.Estado=="FINALIZADO" && x.FechaEvento==new DateTime(2026,10,29,5,3,0) && x.Toneladas==32.85m),type.Name+": actual W-0073 discharge appears on October 29 at 05:03");
  var expected=rows.Where(x=>x.FechaEvento?.Date==expectedDay&&x.Estado=="FINALIZADO").Sum(x=>x.Toneladas);
  Check(status.Where(x=>x.Estado=="FINALIZADO").Sum(x=>x.Toneladas)==expected,"Selected-day tons reconcile to workbook for "+type.Name);
  if(Path.GetFileName(args[0]).Contains("2026-10-30T073546.860")) Check(expected==591.45m,"October 29 reconciles independently to 19 discharges totaling 591.45 tonnes");
  Check(model.ToneladasAlDia==model.Rows.Where(x=>x.FechaEvento?.Date==model.FechaOperativa && x.Estado=="FINALIZADO").Sum(x=>x.Toneladas),"Latest-day daily KPI remains consistent for "+type.Name);
 }
}
