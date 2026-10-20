using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Controllers;
public partial class OperacionesController
{
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
