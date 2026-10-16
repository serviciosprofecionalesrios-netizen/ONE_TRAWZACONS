using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;

var root = Path.Combine(Path.GetTempPath(), "maestros-check-" + Guid.NewGuid());
Environment.SetEnvironmentVariable("PERSISTENT_STORAGE_PATH", root);
var controller = new OperacionesController();
void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
MaestroOperativoViewModel Model(IActionResult result) => (MaestroOperativoViewModel)((ViewResult)result).Model!;
Check(Model(controller.MaestroEquipos()).Registros.Count == 0, "Equipos inicia vacío");
Check(controller.GuardarMaestro(true, ["EQ001", "M001", "Camión", "Marca", "Modelo"]) is RedirectToActionResult, "Registro individual guardado");
Check(Model(controller.GuardarMaestro(true, ["eq001", "M002", "", "", ""])).Errores.Count == 1, "Código duplicado rechazado sin distinguir mayúsculas");
Check(Model(controller.GuardarMaestro(true, ["EQ002", "m001", "", "", ""])).Errores.Count == 1, "Placa duplicada rechazada");
Check(Model(controller.GuardarMaestro(false, ["", "Nombre", "", "", ""])).Errores.Count > 0, "Campos obligatorios validados");
var plantilla = (FileContentResult)controller.PlantillaMaestro(false);
using var package = new ExcelPackage(new MemoryStream(plantilla.FileContents));
var sheet = package.Workbook.Worksheets[0];
Check(sheet.Cells[1,1].Text == "Cedula" && sheet.Cells[1,2].Text == "Nombre", "Plantilla de conductores válida");
sheet.Cells[2,1].Value = "001"; sheet.Cells[2,2].Value = "Ana";
Microsoft.AspNetCore.Http.IFormFile Archivo(byte[] bytes) => new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(bytes), 0, bytes.Length, "archivo", "datos.xlsx");
Check(controller.CargaMasivaMaestro(false, Archivo(package.GetAsByteArray())) is RedirectToActionResult, "Carga Excel guarda conductores");
Check(Model(new OperacionesController().MaestroConductores()).Registros[0][0] == "001", "Persistencia conserva ceros e independencia de maestros");
sheet.Cells[3,1].Value = "002"; sheet.Cells[3,2].Value = "Luis";
Check(Model(controller.CargaMasivaMaestro(false, Archivo(package.GetAsByteArray()))).Errores.Count > 0, "Lote con duplicado rechazado");
Check(Model(controller.MaestroConductores()).Registros.Count == 1, "Lote rechazado no guarda parcialmente");
Check(Model(controller.CargaMasivaMaestro(true, Archivo(plantilla.FileContents))).Errores.Count > 0, "Plantilla incorrecta rechazada");
Check(Model(controller.CargaMasivaMaestro(true, Archivo([1,2,3]))).Errores.Count > 0, "Excel corrupto rechazado");
Console.WriteLine("Datos temporales: " + root);

namespace ITServiceDeskApp.Controllers
{
    public partial class OperacionesController : Controller
    {
        private readonly Microsoft.Extensions.Logging.ILogger<OperacionesController> _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<OperacionesController>.Instance;
        private readonly TestEnvironment _environment = new();
        public OperacionesController() { TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(new Microsoft.AspNetCore.Http.DefaultHttpContext(), new TestTempData()); }
    }
    public sealed class TestEnvironment { public string ContentRootPath => Path.GetTempPath(); }
    public sealed class TestTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
    }
}
