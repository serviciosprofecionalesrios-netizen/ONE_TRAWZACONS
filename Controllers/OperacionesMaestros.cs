using System.Text.Json;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;

namespace ITServiceDeskApp.Controllers;

public partial class OperacionesController
{
    private static readonly object MaestrosLock = new();

    [HttpGet]
    public IActionResult MaestroEquipos() => MostrarMaestro(true);

    [HttpGet]
    public IActionResult MaestroConductores() => MostrarMaestro(false);

    private IActionResult MostrarMaestro(bool equipos, List<string>? errores = null)
    {
        lock (MaestrosLock)
        {
            return View("MaestroOperativo", new MaestroOperativoViewModel
            {
                Equipos = equipos, Registros = LeerMaestro(equipos), Errores = errores ?? new()
            });
        }
    }

    [HttpGet]
    public IActionResult PlantillaMaestro(bool equipos)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Datos");
        var columnas = new MaestroOperativoViewModel { Equipos = equipos }.Columnas;
        for (var c = 0; c < columnas.Length; c++) sheet.Cells[1, c + 1].Value = columnas[c];
        sheet.Cells[1, 1, 1, columnas.Length].Style.Font.Bold = true;
        for (var c = 1; c <= columnas.Length; c++)
        {
            sheet.Column(c).Style.Numberformat.Format = "@";
            sheet.Column(c).Width = 24;
        }
        return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            equipos ? "PlantillaEquipos.xlsx" : "PlantillaConductores.xlsx");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult GuardarMaestro(bool equipos, string[] valores)
        => ImportarFilasMaestro(equipos, new List<string[]> { valores });

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public IActionResult CargaMasivaMaestro(bool equipos, IFormFile? archivo)
    {
        if (archivo == null || archivo.Length == 0 || archivo.Length > 5 * 1024 * 1024 ||
            !string.Equals(Path.GetExtension(archivo.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return MostrarMaestro(equipos, new() { "Seleccione un archivo Excel .xlsx de hasta 5 MB." });

        var filas = new List<string[]>();
        try
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var stream = archivo.OpenReadStream();
            using var package = new ExcelPackage(stream);
            var sheet = package.Workbook.Worksheets.FirstOrDefault();
            var columnas = new MaestroOperativoViewModel { Equipos = equipos }.Columnas;
            if (sheet?.Dimension == null || sheet.Dimension.End.Row > 5001 || sheet.Dimension.End.Column != columnas.Length)
                return MostrarMaestro(equipos, new() { "Use la plantilla y un máximo de 5,000 registros por carga." });
            for (var c = 0; c < columnas.Length; c++)
                if (!string.Equals(sheet.Cells[1, c + 1].Text.Trim(), columnas[c], StringComparison.OrdinalIgnoreCase))
                    return MostrarMaestro(equipos, new() { "Los encabezados deben coincidir con la plantilla descargada." });
            for (var r = 2; r <= sheet.Dimension.End.Row; r++)
            {
                var fila = Enumerable.Range(1, columnas.Length).Select(c => sheet.Cells[r, c].Text.Trim()).ToArray();
                if (fila.Any(v => v.Length > 0)) filas.Add(fila);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "Archivo inválido en carga de maestros operativos");
            return MostrarMaestro(equipos, new() { "No se pudo leer el Excel. Verifique que sea un archivo válido y no esté protegido." });
        }
        return ImportarFilasMaestro(equipos, filas);
    }

    private IActionResult ImportarFilasMaestro(bool equipos, List<string[]> filas)
    {
        lock (MaestrosLock)
        {
            var registros = LeerMaestro(equipos);
            var claves = registros.Select(r => r[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var placas = registros.Where(r => equipos && r[1].Length > 0).Select(r => r[1]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var errores = new List<string>();
            if (filas.Count == 0) errores.Add("El archivo no contiene registros.");
            for (var i = 0; i < filas.Count; i++)
            {
                filas[i] = filas[i].Select(v => (v ?? "").Trim()).ToArray();
                var fila = filas[i];
                if (fila.Length != 5 || string.IsNullOrWhiteSpace(fila[0]) || string.IsNullOrWhiteSpace(fila[1]) || fila.Any(v => v.Length > 150))
                    errores.Add($"Registro {i + 1}: los dos primeros campos son obligatorios; máximo 150 caracteres por campo.");
                else if (!claves.Add(fila[0])) errores.Add($"Registro {i + 1}: código o cédula duplicado ({fila[0]}).");
                else if (equipos && !placas.Add(fila[1])) errores.Add($"Registro {i + 1}: placa duplicada ({fila[1]}).");
            }
            if (errores.Count > 0) return MostrarMaestro(equipos, errores);
            registros.AddRange(filas);
            var path = RutaMaestro(equipos);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporal = path + ".tmp";
            try
            {
                System.IO.File.WriteAllText(temporal, JsonSerializer.Serialize(registros));
                System.IO.File.Move(temporal, path, true);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "No se pudo guardar el maestro operativo");
                return MostrarMaestro(equipos, new() { "No se pudieron guardar los registros. Intente nuevamente." });
            }
            TempData["MaestroMensaje"] = $"Se guardaron {filas.Count} registros correctamente.";
            return RedirectToAction(equipos ? nameof(MaestroEquipos) : nameof(MaestroConductores));
        }
    }

    private List<string[]> LeerMaestro(bool equipos)
    {
        var path = RutaMaestro(equipos);
        return System.IO.File.Exists(path)
            ? JsonSerializer.Deserialize<List<string[]>>(System.IO.File.ReadAllText(path)) ?? new()
            : new();
    }

    private string RutaMaestro(bool equipos)
    {
        var root = Environment.GetEnvironmentVariable("PERSISTENT_STORAGE_PATH");
        return Path.Combine(string.IsNullOrWhiteSpace(root) ? Path.Combine(_environment.ContentRootPath, "Data", "Operaciones") : Path.Combine(root, "operaciones"),
            equipos ? "MaestroEquipos.json" : "MaestroConductores.json");
    }
}
