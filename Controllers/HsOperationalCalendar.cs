using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Hs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Controllers;
public partial class HsController
{
    [HttpGet]
    public async Task<IActionResult> Calendar([FromServices] HsCalendarSource source, DateTime? date, string view = "week", string driver = "", string training = "", string status = "", string site = "")
    {
        driver = driver?.Trim() ?? ""; training = training?.Trim() ?? "";
        status = status?.Trim() ?? ""; site = site?.Trim() ?? "";
        var today = HsCalendarSource.Local(DateTime.UtcNow).Date;
        var anchor = date?.Date ?? today;
        if (anchor.Year < 2000 || anchor.Year > 2100) return BadRequest("Fecha fuera del rango permitido.");
        var model = source.Read(today);
        model.Anchor = anchor; model.View = view == "month" ? "month" : "week";
        model.Driver = driver; model.Training = training; model.Status = status; model.Site = site;
        var first = model.View == "month" ? new DateTime(anchor.Year, anchor.Month, 1) : anchor;
        model.Start = first.AddDays(-((int)first.DayOfWeek + 6) % 7);
        model.End = model.View == "month" ? new DateTime(anchor.Year, anchor.Month, 1).AddMonths(1) : model.Start.AddDays(7);
        if (model.View == "month") model.End = model.End.AddDays((7 - ((int)model.End.DayOfWeek + 6) % 7) % 7);
        var startUtc = DateTime.SpecifyKind(model.Start.AddHours(6), DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(model.End.AddHours(6), DateTimeKind.Utc);
        var inspections = await _context.HsInspections.AsNoTracking().Where(x => x.InspectionDateUtc >= startUtc && x.InspectionDateUtc < endUtc).ToListAsync();
        foreach (var x in inspections) model.Events.Add(new() { Key = $"inspection:{x.Id}", Date = HsCalendarSource.Local(x.InspectionDateUtc), Title = $"{x.Category} · {x.Site}", Group = "Inspecciones y auditorías", Site = x.Site, Status = x.Status, Tone = "info", Url = "/Hs/Inspections" });
        var trainings = await _context.HsTrainingRecords.AsNoTracking().Where(x => x.TrainingDateUtc >= startUtc && x.TrainingDateUtc < endUtc).ToListAsync();
        foreach (var x in trainings)
        {
            // Individual records must resolve to an active official driver. Collective activities have no individual identity.
            var matches = !string.IsNullOrWhiteSpace(x.EmployeeCode)
                ? model.DriverCodes.Where(d => string.Equals(d.Value, x.EmployeeCode.Trim(), StringComparison.OrdinalIgnoreCase)).Select(d => new KeyValuePair<string,string>(d.Key, model.Drivers[d.Key])).ToList()
                : model.Drivers.Where(d => string.Equals(d.Value, x.EmployeeName?.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if ((!string.IsNullOrWhiteSpace(x.EmployeeName) || !string.IsNullOrWhiteSpace(x.EmployeeCode)) && matches.Count != 1) continue;
            model.Events.Add(new() { Key = $"training:{x.Id}", Date = HsCalendarSource.Local(x.TrainingDateUtc), Title = x.Topic, Training = x.Topic, Driver = matches.Count == 1 ? matches[0].Value : x.Audience, Performed = HsCalendarSource.Local(x.TrainingDateUtc).ToString("dd/MM/yyyy"), DriverId = matches.FirstOrDefault().Key ?? "", Code = x.EmployeeCode ?? "", Site = x.Site, Group = "Capacitaciones programadas", Status = x.Result, Tone = "primary", Url = $"/Hs/Trainings#training-{x.Id}" });
        }
        var actions = await _context.HsCorrectiveActions.AsNoTracking().Where(x => x.DueDateUtc >= startUtc && x.DueDateUtc < endUtc && x.Status != "Cerrada").ToListAsync();
        foreach (var x in actions) model.Events.Add(new() { Key = $"action:{x.Id}", Date = HsCalendarSource.Local(x.DueDateUtc), Title = x.Title, Status = x.Status, Tone = "warning", Url = $"/Hs/ActionEdit/{x.Id}" });
        var permits = await _context.HsWorkPermits.AsNoTracking().Where(x => x.EndAtUtc >= startUtc && x.EndAtUtc < endUtc).ToListAsync();
        foreach (var x in permits) model.Events.Add(new() { Key = $"permit:{x.Id}", Date = HsCalendarSource.Local(x.EndAtUtc), Title = $"{x.PermitType} · {x.Area}", Status = x.Status, Tone = "secondary", Url = $"/Hs/PermitEdit/{x.Id}" });
        var docs = await _context.HsDocuments.AsNoTracking().Where(x => x.ReviewDateUtc >= startUtc && x.ReviewDateUtc < endUtc).ToListAsync();
        foreach (var x in docs) model.Events.Add(new() { Key = $"document:{x.Id}", Date = HsCalendarSource.Local(x.ReviewDateUtc!.Value), Title = $"{x.Code} · {x.Title}", Status = x.Status, Tone = "secondary", Url = "/Hs/Documents" });
        model.Sites = model.Events.Select(x => x.Site).Where(x => x.Length > 0).Distinct().Order().ToList();
        model.Trainings = model.Trainings.Concat(model.Events.Select(x => x.Training).Where(x => x.Length > 0)).Distinct().Order().ToList();
        bool Matches(HsOperationalEvent x) => (driver == "" || x.DriverId == driver) && (training == "" || x.Training == training) && (status == "" || x.Status == status) && (site == "" || x.Site == site);
        model.Alerts = model.Alerts.Where(Matches).ToList();
        var expiry = model.Events.Where(x => x.IsTraining && x.Days.HasValue && Matches(x)).ToList();
        model.ActiveDrivers = driver == "" ? model.ActiveDrivers : model.Drivers.ContainsKey(driver) ? 1 : 0;
        model.Valid = expiry.Count(x => x.Days > 30); model.Due = expiry.Count(x => x.Days > 0 && x.Days <= 30); model.Expired = expiry.Count(x => x.Days <= 0);
        model.Events = model.Events.Where(x => x.Date >= model.Start && x.Date < model.End && Matches(x)).OrderBy(x => x.Date).ToList();
        return View(model);
    }
}
