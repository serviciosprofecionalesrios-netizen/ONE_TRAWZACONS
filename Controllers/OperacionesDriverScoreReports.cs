using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.Controllers;

public partial class OperacionesController
{
    [HttpGet]
    public async Task<IActionResult> DriverScoreCases(string? period, string? driver, CancellationToken cancellationToken, DateTime? from = null, DateTime? to = null)
    {
        var result = await DriverScore(period, from, to, driver, cancellationToken: cancellationToken);
        if (result is not ViewResult { Model: DriverScoreViewModel model }) return result;
        var cases = model.Cases;
        return View(new DriverScoreCasesViewModel(model.SelectedPeriod, driver, model.AllPeriodEvents, cases) { From = model.UsingDateRange ? model.From : null, To = model.UsingDateRange ? model.To : null });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDriverScoreCase(string eventKey, string status, string? responsible, string? notes, IFormFile? evidence, string? period, string? driver, CancellationToken cancellationToken, DateTime? from = null, DateTime? to = null)
    {
        var result = await DriverScore(period, from, to, driver, cancellationToken: cancellationToken);
        if (result is not ViewResult { Model: DriverScoreViewModel model } || !model.AllPeriodEvents.Any(x => x.CaseKey == eventKey)) return BadRequest();
        if (!new[] { "Pendiente", "En seguimiento", "Cerrado" }.Contains(status) || responsible?.Length > 150 || notes?.Length > 1000) return BadRequest();
        var item = await _context.DriverScoreCases.FindAsync(new object[] { eventKey }, cancellationToken);
        if (evidence != null && (evidence.Length == 0 || evidence.Length > 10 * 1024 * 1024 || !new[] { ".pdf", ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(evidence.FileName).ToLowerInvariant())))
        { TempData["DriverScoreCaseError"] = "La evidencia debe ser PDF, JPG o PNG, de hasta 10 MB."; return RedirectToAction(nameof(DriverScoreCases), new { period, driver, from, to }); }
        if (!DriverScoreCasePolicy.CanSave(status, responsible, evidence != null || item?.Evidence != null))
        { TempData["DriverScoreCaseError"] = "En seguimiento requiere responsable. Cerrado requiere responsable y evidencia."; return RedirectToAction(nameof(DriverScoreCases), new { period, driver, from, to }); }
        if (item == null) { item = new DriverScoreCase { EventKey = eventKey }; _context.DriverScoreCases.Add(item); }
        if (evidence != null) { using var buffer = new MemoryStream(); await evidence.CopyToAsync(buffer, cancellationToken); item.Evidence = buffer.ToArray(); item.EvidenceName = Path.GetFileName(evidence.FileName); }
        item.Status = status; item.Responsible = responsible?.Trim(); item.Notes = notes?.Trim(); item.UpdatedAt = DateTime.UtcNow; item.UpdatedBy = User.Identity?.Name;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["DriverScoreOk"] = "Seguimiento del caso actualizado.";
        return RedirectToAction(nameof(DriverScoreCases), new { period, driver, from, to });
    }

    [HttpGet]
    public async Task<IActionResult> DriverScoreCaseEvidence(string eventKey, CancellationToken cancellationToken)
    {
        var item = await _context.DriverScoreCases.AsNoTracking().SingleOrDefaultAsync(x => x.EventKey == eventKey, cancellationToken);
        if (item?.Evidence == null) return NotFound();
        return File(item.Evidence, "application/octet-stream", item.EvidenceName ?? "evidencia");
    }

    [HttpGet]
    public async Task<IActionResult> DriverScoreEvidence(int id, CancellationToken cancellationToken)
    {
        var record = await _context.DriverScoreManualEvents.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.CoachingEvidence, x.CoachingEvidenceName }).SingleOrDefaultAsync(cancellationToken);
        if (record?.CoachingEvidence == null || string.IsNullOrWhiteSpace(record.CoachingEvidenceName)) return NotFound();
        var contentType = Path.GetExtension(record.CoachingEvidenceName).ToLowerInvariant() switch
        { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => "application/octet-stream" };
        return File(record.CoachingEvidence, contentType, record.CoachingEvidenceName);
    }
    [HttpGet]
    public async Task<IActionResult> DriverScorePdf(string? period, string? driver, CancellationToken cancellationToken, DateTime? from = null, DateTime? to = null)
    {
        var result = await DriverScore(period, from, to, driver, cancellationToken: cancellationToken);
        if (result is not ViewResult { Model: DriverScoreViewModel model }) return result;
        return File(DriverScorePdfReportService.Generate(model), "application/pdf", $"DriverScore_{model.SelectedPeriod}.pdf");
    }
}
