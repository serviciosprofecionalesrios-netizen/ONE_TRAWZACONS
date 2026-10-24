using System.Reflection;
using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Operaciones;
using OfficeOpenXml;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
var day = new DateTime(2026, 10, 24);
SeguimientoToneladasRowViewModel Row(string unit, int offset, string state, decimal tons = 0) => new() { Equipo=unit, FechaEvento=day.AddDays(offset), Estado=state, Toneladas=tons, Procedencia="TRITON", Ruta="TRITON - DESTINO", Viajes=1 };
var sample = new[] {Row("W-0119", -1,"EN RUTA"), Row("W-0105", -1,"EN RUTA"),Row("CLOSED",-2,"EN RUTA"),Row("CLOSED",-1,"FINALIZADO",30),Row("TODAY",0,"FINALIZADO",32),Row("FUTURE",1,"EN RUTA")};
var selected=TonnageStatusRows.Select(sample,day);
Check(selected.Count==3 && selected.Any(x=>x.Equipo=="W-0119") && selected.Any(x=>x.Equipo=="W-0105"),"Previous-day open trips remain visible, closed and future trips do not carry");
Check(selected.Where(x=>x.Estado=="FINALIZADO").Sum(x=>x.Toneladas)==32,"Daily discharged tonnage is unchanged");
Check(TonnageStatusRows.Select(sample.Append(Row("W-0119",0,"FINALIZADO",31)),day).Count(x=>x.Equipo=="W-0119")==1,"Current-day trip replaces prior-day open trip without duplicate");
if(args.Length>0) {
 ExcelPackage.LicenseContext=LicenseContext.NonCommercial;
 using var package=new ExcelPackage(new FileInfo(args[0]));
 var parse=typeof(OperacionesController).GetMethod("ParseToneladasRowsFromWorkbook",BindingFlags.Static|BindingFlags.NonPublic)!;
 object?[] parseArgs=[package.Workbook,null,null];
 var rows=(List<SeguimientoToneladasRowViewModel>)parse.Invoke(null,parseArgs)!;
 Check(rows.Count>0,"Actual TiHouse workbook parsed");
 foreach(var type in new[]{typeof(OperacionesController),typeof(OperacionesSeguimientoStore)}) {
  var build=type.GetMethod("BuildToneladasModel",BindingFlags.Static|BindingFlags.NonPublic)!;
  var model=(SeguimientoToneladasViewModel)build.Invoke(null,[rows])!;
  Check(model.FechaOperativa==day,"Operational date is October 24");
  var status=TonnageStatusRows.Select(model.Rows,model.FechaOperativa);
  foreach(var unit in new[]{"W-0119","W-0105"}) Check(status.Any(x=>x.Equipo==unit&&x.Estado=="EN RUTA"),type.Name+": "+unit+" remains in route");
  var expected=rows.Where(x=>x.FechaEvento?.Date==day&&x.Estado=="FINALIZADO").Sum(x=>x.Toneladas);
  Check(model.ToneladasAlDia==expected,"Daily tons reconcile to workbook for "+type.Name);
 }
}
