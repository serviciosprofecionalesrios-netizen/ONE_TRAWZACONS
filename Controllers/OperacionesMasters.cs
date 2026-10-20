using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Controllers;
public partial class OperacionesController
{
    [HttpGet]
    public IActionResult MaestroPlantilla(string type)
    {
        if (!OperationalMasterCatalog.Kinds.Contains(type)) return NotFound();
        OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        using var package = new OfficeOpenXml.ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Propietario");
        var headers = new[] { "#" }.Concat(OperationalMasterCatalog.Columns(type)).ToArray();
        for(var i = 0; i < headers.Length; i++) { sheet.Cells[1, i + 1].Value = headers[i]; sheet.Column(i + 1).Width = 28; sheet.Column(i + 1).Style.Numberformat.Format = "@"; }
        sheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
        return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Maestro_{type}.xlsx");
    }
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> MaestroImportar(string type, IFormFile? archivo, CancellationToken cancellationToken)
    {
        if (!ITServiceDeskApp.Security.UserAccessProfiles.CanMaintainOperationalMasters(User)) return Forbid();
        if (!OperationalMasterCatalog.Kinds.Contains(type)) return NotFound();
        try
        {
            if (archivo == null || archivo.Length == 0 || archivo.Length > 5 * 1024 * 1024 || !Path.GetExtension(archivo.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Selecciona un Excel .xlsx de hasta 5 MB.");
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            using var stream = archivo.OpenReadStream(); using var package = new OfficeOpenXml.ExcelPackage(stream);
            var sheet = package.Workbook.Worksheets.FirstOrDefault();
            var headers = OperationalMasterCatalog.Columns(type);
            if (sheet?.Dimension == null || sheet.Dimension.End.Row > 5001) throw new ArgumentException("El archivo debe contener hasta 5,000 registros.");
            var start = sheet.Cells[1,1].Text.Trim() == "#" ? 2 : 1;
            if (sheet.Dimension.End.Column != headers.Length + start - 1) throw new ArgumentException("Las columnas deben coincidir con la plantilla del maestro seleccionado.");
            for(var i = 0; i < headers.Length; i++)
                if (OperationalMasterCatalog.Key("", sheet.Cells[1,start+i].Text) != OperationalMasterCatalog.Key("", headers[i])) throw new ArgumentException("Los encabezados deben coincidir con la plantilla.");
            var keys = (await _context.OperationalMasterRecords.Where(x => x.Kind == type).Select(x => x.RecordKey).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = new List<OperationalMasterRecord>(); var skipped = 0;
            for(var r = 2; r <= sheet.Dimension.End.Row; r++)
            {
                var values = Enumerable.Range(start, headers.Length).Select(c => sheet.Cells[r,c].Text.Trim()).ToArray();
                if (values.All(string.IsNullOrWhiteSpace)) continue;
                var row = OperationalMasterCatalog.FromValues(type, values);
                if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(row, new System.ComponentModel.DataAnnotations.ValidationContext(row), new List<System.ComponentModel.DataAnnotations.ValidationResult>(), true) || (type == "equipos" && string.IsNullOrWhiteSpace(row.Plate))) throw new ArgumentException($"Fila {r}: completa los campos obligatorios y revisa su longitud. No se guardó la carga.");
                row.RecordKey = OperationalMasterCatalog.Key(type, row.Name);
                if (keys.Add(row.RecordKey)) added.Add(row); else skipped++;
            }
            _context.OperationalMasterRecords.AddRange(added); await _context.SaveChangesAsync(cancellationToken);
            TempData["MasterOk"] = $"Carga completada: {added.Count} nuevos; {skipped} duplicados omitidos. Se conservaron los registros existentes.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not OperationCanceledException)
        { _logger.LogWarning(ex, "Error al importar maestro operativo {Type}", type); TempData["MasterError"] = ex is ArgumentException ? ex.Message : "No se pudo importar el archivo. Verifica el formato y vuelve a intentar."; }
        return RedirectToAction(nameof(Maestros), new { type });
    }
    [HttpGet]
    public async Task<IActionResult> Maestros(string type = "equipos", string? q = null, int page = 1, CancellationToken cancellationToken = default)
    {
        if (!OperationalMasterCatalog.Kinds.Contains(type)) return NotFound();
        var query = _context.OperationalMasterRecords.AsNoTracking().Where(x => x.Kind == type);
        if (!string.IsNullOrWhiteSpace(q)) { var text = q.Trim(); query = query.Where(x => x.Name.Contains(text) || (x.Plate != null && x.Plate.Contains(text)) || x.Owner.Contains(text)); }
        var count = await query.CountAsync(cancellationToken);
        var pagination = new InventoryTablePage(count, page, 25);
        ViewData["MasterType"] = type; ViewData["Search"] = q; ViewData["Pagination"] = pagination;
        return View(await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(pagination.Skip).Take(25).ToListAsync(cancellationToken));
    }
    [HttpGet]
    public IActionResult MaestroNuevo(string type = "equipos") => !ITServiceDeskApp.Security.UserAccessProfiles.CanMaintainOperationalMasters(User) ? Forbid() : OperationalMasterCatalog.Kinds.Contains(type) ? View(new OperationalMasterRecord { Kind = type }) : NotFound();
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MaestroNuevo([Bind("Kind,Name,Plate,Tenure,Owner")] OperationalMasterRecord model, CancellationToken cancellationToken)
    {
        if (!ITServiceDeskApp.Security.UserAccessProfiles.CanMaintainOperationalMasters(User)) return Forbid();
        if (!OperationalMasterCatalog.Kinds.Contains(model.Kind)) return BadRequest();
        if (model.Kind == "equipos" && string.IsNullOrWhiteSpace(model.Plate)) ModelState.AddModelError(nameof(model.Plate), "La placa del cabezal es obligatoria.");
        if (!ModelState.IsValid) return View(model);
        model.Name = model.Name.Trim(); model.Plate = model.Kind == "equipos" ? model.Plate?.Trim() : null;
        model.Tenure = model.Tenure.Trim(); model.Owner = model.Owner.Trim();
        model.RecordKey = OperationalMasterCatalog.Key(model.Kind, model.Name);
        if (await _context.OperationalMasterRecords.AnyAsync(x => x.RecordKey == model.RecordKey, cancellationToken))
        { ModelState.AddModelError(nameof(model.Name), "Ya existe un registro con ese nombre o identificación."); return View(model); }
        _context.OperationalMasterRecords.Add(model);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "No se pudo guardar. Comprueba que el registro no exista e intenta nuevamente."); return View(model); }
        TempData["MasterOk"] = "Registro agregado correctamente.";
        return RedirectToAction(nameof(Maestros), new { type = model.Kind });
    }
}
