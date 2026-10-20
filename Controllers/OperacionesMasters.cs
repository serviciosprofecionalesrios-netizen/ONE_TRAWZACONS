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
        if (!ITServiceDeskApp.Security.UserAccessProfiles.CanMaintainMaster(User, type)) return Forbid();
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
            var keys = (await _context.OperationalMasterRecords.Where(x => x.Kind == type).Select(x => x.Name).ToListAsync(cancellationToken)).Select(x => OperationalMasterCatalog.Key(type, x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = new List<OperationalMasterRecord>(); var skipped = 0;
            for(var r = 2; r <= sheet.Dimension.End.Row; r++)
            {
                var values = Enumerable.Range(start, headers.Length).Select(c => sheet.Cells[r,c].Text.Trim()).ToArray();
                if (values.All(string.IsNullOrWhiteSpace)) continue;
                var row = OperationalMasterCatalog.FromValues(type, values);
                if (type == "conductores" && OperationalMasterCatalog.PrepareDriver(row) is string nameError) throw new ArgumentException($"Fila {r}: {nameError} No se guardó la carga.");
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
    public async Task<IActionResult> Maestros(string type = "equipos", string? q = null, int page = 1, string status = "activos", CancellationToken cancellationToken = default)
    {
        if (!OperationalMasterCatalog.Kinds.Contains(type)) return NotFound();
        var query = _context.OperationalMasterRecords.AsNoTracking().Where(x => x.Kind == type);
        if (type == "conductores" && status != "todos") query = query.Where(x => x.IsActive == (status != "inactivos"));
        ViewData["Status"] = status;
        if (!string.IsNullOrWhiteSpace(q)) { var text = q.Trim(); query = query.Where(x => x.Name.Contains(text) || (x.Plate != null && x.Plate.Contains(text)) || x.Owner.Contains(text)); }
        var count = await query.CountAsync(cancellationToken);
        var pagination = new InventoryTablePage(count, page, 25);
        ViewData["MasterType"] = type; ViewData["Search"] = q; ViewData["Pagination"] = pagination;
        return View(await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(pagination.Skip).Take(25).ToListAsync(cancellationToken));
    }
    [HttpGet]
    public IActionResult MaestroNuevo(string type = "equipos") => !ITServiceDeskApp.Security.UserAccessProfiles.CanMaintainMaster(User, type) ? Forbid() : OperationalMasterCatalog.Kinds.Contains(type) ? View(new OperationalMasterRecord { Kind = type }) : NotFound();
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MaestroNuevo([Bind("Kind,Name,Plate,Tenure,Owner,FirstName,SecondName,FirstSurname,SecondSurname")] OperationalMasterRecord model, CancellationToken cancellationToken)
    {
        if (!ITServiceDeskApp.Security.UserAccessProfiles.CanMaintainMaster(User, model.Kind)) return Forbid();
        if (!OperationalMasterCatalog.Kinds.Contains(model.Kind)) return BadRequest();
        if (model.Kind == "conductores") ValidateDriverName(model);
        if (model.Kind == "equipos" && string.IsNullOrWhiteSpace(model.Plate)) ModelState.AddModelError(nameof(model.Plate), "La placa del cabezal es obligatoria.");
        if (!ModelState.IsValid) return View(model);
        model.Name = model.Name.Trim(); model.Plate = model.Kind == "equipos" ? model.Plate?.Trim() : null;
        model.Tenure = model.Tenure.Trim(); model.Owner = model.Owner.Trim();
        model.RecordKey = OperationalMasterCatalog.Key(model.Kind, model.Name);
        if (await DriverOrMasterDuplicate(model, 0, cancellationToken))
        { ModelState.AddModelError(nameof(model.Name), "Ya existe un registro con ese nombre o identificación."); return View(model); }
        _context.OperationalMasterRecords.Add(model);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "No se pudo guardar. Comprueba que el registro no exista e intenta nuevamente."); return View(model); }
        TempData["MasterOk"] = "Registro agregado correctamente.";
        return RedirectToAction(nameof(Maestros), new { type = model.Kind });
    }
    private void ValidateDriverName(OperationalMasterRecord model)
    {
        ModelState.Remove(nameof(model.Name)); // The server constructs the full name from the four required parts.
        if (OperationalMasterCatalog.PrepareDriver(model) is string error) ModelState.AddModelError(nameof(model.Name), error);
    }

    private async Task<bool> DriverOrMasterDuplicate(OperationalMasterRecord model, int exceptId, CancellationToken cancellationToken)
    {
        var names = await _context.OperationalMasterRecords.AsNoTracking().Where(x => x.Kind == model.Kind && x.Id != exceptId).Select(x => x.Name).ToListAsync(cancellationToken);
        return names.Any(x => OperationalMasterCatalog.Key(model.Kind, x) == OperationalMasterCatalog.Key(model.Kind, model.Name));
    }

    [HttpGet]
    public async Task<IActionResult> ConductorEditar(int id, CancellationToken cancellationToken)
    {
        if (!ITServiceDeskApp.Security.UserAccessProfiles.CanMaintainDrivers(User)) return Forbid();
        var row = await _context.OperationalMasterRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Kind == "conductores", cancellationToken);
        return row == null ? NotFound() : View("MaestroNuevo", row);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConductorEditar(int id, [Bind("FirstName,SecondName,FirstSurname,SecondSurname,Tenure,Owner")] OperationalMasterRecord model, CancellationToken cancellationToken)
    {
        if (!ITServiceDeskApp.Security.UserAccessProfiles.CanMaintainDrivers(User)) return Forbid();
        var row = await _context.OperationalMasterRecords.SingleOrDefaultAsync(x => x.Id == id && x.Kind == "conductores", cancellationToken);
        if (row == null) return NotFound();
        model.Id = row.Id; model.Kind = row.Kind;
        ValidateDriverName(model);
        if (await DriverOrMasterDuplicate(model, id, cancellationToken)) ModelState.AddModelError(nameof(model.Name), "Ya existe un conductor con ese nombre completo, incluso si está inactivo.");
        if (!ModelState.IsValid) return View("MaestroNuevo", model);
        row.FirstName = model.FirstName; row.SecondName = model.SecondName;
        row.FirstSurname = model.FirstSurname; row.SecondSurname = model.SecondSurname;
        row.Name = model.Name; row.Tenure = model.Tenure.Trim(); row.Owner = model.Owner.Trim();
        row.RecordKey = OperationalMasterCatalog.Key(row.Kind, row.Name);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { ModelState.AddModelError("", "No se pudo guardar. Comprueba que el conductor no exista e intenta nuevamente."); return View("MaestroNuevo", model); }
        TempData["MasterOk"] = $"Conductor {row.DisplayId} actualizado. Se conservó su ID y su historial.";
        return RedirectToAction(nameof(Maestros), new { type = "conductores", status = "todos" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConductorEstado(int id, bool active, CancellationToken cancellationToken)
    {
        if (!ITServiceDeskApp.Security.UserAccessProfiles.CanMaintainDrivers(User)) return Forbid();
        var row = await _context.OperationalMasterRecords.SingleOrDefaultAsync(x => x.Id == id && x.Kind == "conductores", cancellationToken);
        if (row == null) return NotFound();
        row.IsActive = active;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["MasterOk"] = $"Conductor {row.DisplayId} {(active ? "reactivado" : "desactivado")}. Se conserva su historial.";
        return RedirectToAction(nameof(Maestros), new { type = "conductores", status = "todos" });
    }

}
