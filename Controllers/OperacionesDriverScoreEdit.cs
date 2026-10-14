using ITServiceDeskApp.Models;
using ITServiceDeskApp.Security;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace ITServiceDeskApp.Controllers;
public partial class OperacionesController
{
    private bool CanEditDriverScoreStepOne() => UserAccessProfiles.IsAdministrator(User.FindFirst(ClaimTypes.Email)?.Value);
    private static DriverScoreEventViewModel ManualEventView(DriverScoreManualEvent r) =>
        new(r.EventAt, r.EventType, r.Driver, r.Vehicle, r.Group ?? "", r.Location ?? "", r.Observation ?? "", r.TimelyManaged, r.CoachingCompleted) { ManualId = r.Id };

    [HttpGet]
    public async Task<IActionResult> EditDriverScoreEvent(int id, CancellationToken cancellationToken)
    {
        if (!CanEditDriverScoreStepOne()) return Forbid();
        var record = await _context.DriverScoreManualEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record == null) return NotFound();
        ViewData["ManualId"] = id;
        return View(new DriverScoreEventCreateViewModel { EventAt = record.EventAt, Driver = record.Driver, Vehicle = record.Vehicle, EventType = record.EventType, Group = record.Group, Location = record.Location, Observation = record.Observation, CoachingCompleted = record.CoachingCompleted });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDriverScoreEvent(int id, DriverScoreEventCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!CanEditDriverScoreStepOne()) return Forbid();
        ModelState.Remove(nameof(model.Site));
        if (model.Driver.Length > 160 || model.Vehicle.Length > 40 || model.EventType.Length > 120 || model.Group?.Length > 120 || model.Location?.Length > 80 || model.Observation?.Length > 1000)
            ModelState.AddModelError(string.Empty, "Uno de los campos supera la longitud permitida.");
        if (!ModelState.IsValid) { ViewData["ManualId"] = id; return View(model); }
        var record = await _context.DriverScoreManualEvents.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record == null) return NotFound();
        var oldKey = ManualEventView(record).CaseKey;
        var item = await _context.DriverScoreCases.FindAsync(new object[] { oldKey }, cancellationToken);
        record.EventAt = model.EventAt; record.Driver = model.Driver.Trim(); record.Vehicle = model.Vehicle.Trim(); record.EventType = model.EventType.Trim();
        record.Group = model.Group?.Trim(); record.Location = model.Location?.Trim(); record.Observation = model.Observation?.Trim(); record.CoachingCompleted = model.CoachingCompleted;
        record.TimelyManaged = record.CreatedAt.AddHours(-6) <= record.EventAt.AddMinutes(10);
        var newKey = ManualEventView(record).CaseKey;
        if (item != null && oldKey != newKey)
        {
            _context.DriverScoreCases.Remove(item);
            _context.DriverScoreCases.Add(new DriverScoreCase { EventKey = newKey, Site = item.Site, Status = item.Status, Responsible = item.Responsible, Notes = item.Notes, Evidence = item.Evidence, EvidenceName = item.EvidenceName, UpdatedAt = item.UpdatedAt, UpdatedBy = item.UpdatedBy });
        }
        await _context.SaveChangesAsync(cancellationToken);
        TempData["DriverScoreOk"] = "Paso 1 actualizado por el administrador. Se conserva el seguimiento y su evidencia.";
        return RedirectToAction(nameof(DriverScoreCases), new { period = record.EventAt.ToString("yyyy-MM"), driver = record.Driver });
    }
}
