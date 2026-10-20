using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;

var root = Path.Combine(Path.GetTempPath(), "maestros-check-" + Guid.NewGuid());
Environment.SetEnvironmentVariable("PERSISTENT_STORAGE_PATH", root);
var controller = NewController();
void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
MaestroOperativoViewModel Model(IActionResult result) => (MaestroOperativoViewModel)((ViewResult)result).Model!;
Check(Model(controller.MaestroEquipos()).Registros.Count == 0, "Equipos inicia vacío");
Check(controller.GuardarMaestro(true, ["EQ001", "M001", "Camión", "Marca", "Modelo"]) is RedirectToActionResult, "Registro individual guardado");
Check(Model(controller.GuardarMaestro(true, ["eq001", "M002", "", "", ""])).Errores.Count == 1, "Código duplicado rechazado sin distinguir mayúsculas");
Check(Model(controller.GuardarMaestro(true, ["EQ002", "m001", "", "", ""])).Errores.Count == 1, "Placa duplicada rechazada");
Check(controller.MaestroConductores() is RedirectToActionResult redirect && redirect.ActionName == "Maestros", "Conductores usa el catálogo actualizado");
Check(controller.GuardarMaestro(false, ["001", "Nombre", "", "", ""]) is ForbidResult, "Ruta anterior no permite altas de conductores sin autorización");
var plantilla = (FileContentResult)controller.PlantillaMaestro(false);
using var package = new ExcelPackage(new MemoryStream(plantilla.FileContents));
var sheet = package.Workbook.Worksheets[0];
Check(sheet.Cells[1,2].Text == "Primer nombre" && sheet.Cells[1,5].Text == "Segundo apellido", "Plantilla exige dos nombres y dos apellidos");
Microsoft.AspNetCore.Http.IFormFile Archivo(byte[] bytes) => new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(bytes), 0, bytes.Length, "archivo", "datos.xlsx");
Check(controller.CargaMasivaMaestro(false, Archivo(package.GetAsByteArray())) is ForbidResult, "Importación anterior de conductores bloqueada");
Check(Model(controller.CargaMasivaMaestro(true, Archivo(plantilla.FileContents))).Errores.Count > 0, "Plantilla incorrecta rechazada");
Check(Model(controller.CargaMasivaMaestro(true, Archivo([1,2,3]))).Errores.Count > 0, "Excel corrupto rechazado");
Console.WriteLine("Datos temporales: " + root);

OperacionesController NewController() {
 var http=new Microsoft.AspNetCore.Http.DefaultHttpContext();
 return new OperacionesController(null!,null!,null!,null!,null!,null!,null!,null!,null!,Microsoft.Extensions.Logging.Abstractions.NullLogger<OperacionesController>.Instance,new TestEnvironment(),null!) {
  ControllerContext = new ControllerContext { HttpContext = http },
  TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http,new TestTempData())
 };
}
sealed class TestEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment {
 public string ContentRootPath {get;set;} = Path.GetTempPath();
 public string WebRootPath {get;set;} = Path.GetTempPath();
 public string EnvironmentName {get;set;} = "Testing";
 public string ApplicationName {get;set;} = "Check";
 public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider {get;set;} = new Microsoft.Extensions.FileProviders.NullFileProvider();
 public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider {get;set;} = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
sealed class TestTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider {
 public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
 public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
}
