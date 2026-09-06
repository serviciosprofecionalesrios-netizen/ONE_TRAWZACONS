using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.MaintenanceReports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class MaintenanceReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public MaintenanceReportsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? startDate,
            DateTime? endDate,
            string? site,
            string? failureCategory)
        {
            var model = await BuildReportAsync(startDate, endDate, site, failureCategory);
            return View(model);
        }

        [HttpGet]
        public IActionResult CroquisCamion()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCroquisCamion([FromBody] TruckSketchInspectionSaveRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.EquipmentCode))
            {
                return BadRequest(new { message = "Debe indicar el codigo del camion o equipo." });
            }

            var inspectionDate = DateTime.Today;
            if (!string.IsNullOrWhiteSpace(request.InspectionDate) &&
                DateTime.TryParse(request.InspectionDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDate))
            {
                inspectionDate = parsedDate.Date;
            }

            var rows = request.Rows ?? new List<TruckSketchInspectionRow>();
            var inspectedRows = rows
                .Where(x => !string.IsNullOrWhiteSpace(x.Status) || !string.IsNullOrWhiteSpace(x.Note))
                .ToList();
            var totalEvaluated = inspectedRows.Count(x => !string.IsNullOrWhiteSpace(x.Status));
            var findings = inspectedRows.Count(x =>
                x.Status == "Revisar" ||
                x.Status == "Malo" ||
                x.Status == "Debe cambiar");
            var critical = inspectedRows.Count(x => x.Status == "Malo" || x.Status == "Debe cambiar");
            var good = inspectedRows.Count(x => x.Status == "Bueno");
            var score = totalEvaluated == 0
                ? 100m
                : Math.Round((decimal)good / totalEvaluated * 100m, 2);

            var details = inspectedRows
                .Take(40)
                .Select(x => $"{x.Zone} / {x.Component}: {x.Status}{(string.IsNullOrWhiteSpace(x.Note) ? string.Empty : $" - {x.Note}")}");
            var notes = string.Join(Environment.NewLine, new[]
            {
                $"Conductor: {NullIfEmpty(request.DriverName) ?? "No indicado"}",
                $"Mecanico: {NullIfEmpty(request.MechanicName) ?? "No indicado"}",
                $"Resumen: {good} buenos, {findings} hallazgo(s), {critical} critico(s).",
                string.Join(Environment.NewLine, details)
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            if (notes.Length > 2000)
            {
                notes = notes[..2000];
            }

            var entity = new HsInspection
            {
                InspectionNumber = await GenerateNextNumberAsync("INSP", _context.HsInspections.Select(x => x.InspectionNumber)),
                InspectionDateUtc = DateTime.SpecifyKind(inspectionDate, DateTimeKind.Local).ToUniversalTime(),
                Site = "Mantenimiento",
                Area = request.EquipmentCode.Trim(),
                EquipmentCode = request.EquipmentCode.Trim(),
                Inspector = string.IsNullOrWhiteSpace(request.MechanicName) ? CurrentUserName() : request.MechanicName.Trim(),
                Category = "Croquis camion",
                ScorePercent = score,
                FindingsCount = findings,
                CriticalFindingsCount = critical,
                Status = "Completada",
                Notes = notes,
                CreatedBy = CurrentUserName(),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _context.HsInspections.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Inspeccion {entity.InspectionNumber} guardada.",
                inspectionNumber = entity.InspectionNumber
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            DateTime? startDate,
            DateTime? endDate,
            string? site,
            string? failureCategory)
        {
            var model = await BuildReportAsync(startDate, endDate, site, failureCategory);

            var csv = new StringBuilder();
            csv.AppendLine("RESUMEN");
            csv.AppendLine("Metrica,Valor");
            csv.AppendLine($"Total Tickets,{model.TotalMaintenanceTickets}");
            csv.AppendLine($"Preventivos,{model.PreventiveTickets}");
            csv.AppendLine($"Correctivos,{model.CorrectiveTickets}");
            csv.AppendLine($"Otros,{model.OtherTickets}");
            csv.AppendLine($"Eventos Falla-Falla,{model.FailureToFailureEvents}");
            csv.AppendLine($"Promedio KM Falla-Falla,{model.AverageKmBetweenFailures?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty}");
            csv.AppendLine();

            csv.AppendLine("FALLA A FALLA");
            csv.AppendLine("Unidad,Ticket Anterior,Fecha Reparacion,KM Salida,Ticket Actual,Fecha Falla,KM Falla,KM Entre Fallas,Dias");
            foreach (var row in model.FailureToFailureRows)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(row.UnitCode),
                    EscapeCsv(row.PreviousTicketNumber),
                    EscapeCsv(row.PreviousRepairDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.PreviousExitKm.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.CurrentTicketNumber),
                    EscapeCsv(row.CurrentFailureDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.CurrentFailureKm.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.KmBetweenFailures.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.DaysBetweenFailures.ToString(CultureInfo.InvariantCulture))));
            }
            csv.AppendLine();

            csv.AppendLine("ELEMENTOS MAS DANADOS");
            csv.AppendLine("Categoria,Elemento,Total");
            foreach (var row in model.TopDamagedElements)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(row.FailureCategory),
                    EscapeCsv(row.DamagedElement),
                    EscapeCsv(row.Total.ToString(CultureInfo.InvariantCulture))));
            }
            csv.AppendLine();

            csv.AppendLine("PREVENTIVO VS CORRECTIVO");
            csv.AppendLine("Tipo,Total,Promedio Resolucion (h)");
            foreach (var row in model.MaintenanceTypeSummary)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(row.MaintenanceType),
                    EscapeCsv(row.Total.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(row.AverageResolutionHours.ToString("0.0", CultureInfo.InvariantCulture))));
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"ReporteriaMantenimiento_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            DateTime? startDate,
            DateTime? endDate,
            string? site,
            string? failureCategory)
        {
            var model = await BuildReportAsync(startDate, endDate, site, failureCategory);
            var bytes = MaintenanceReportsPdfReportService.GeneratePdf(model, _environment.WebRootPath);
            return File(bytes, "application/pdf", $"ReporteriaMantenimiento_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
        }

        private async Task<MaintenanceReportsViewModel> BuildReportAsync(
            DateTime? startDate,
            DateTime? endDate,
            string? site,
            string? failureCategory)
        {
            var nowUtc = DateTime.UtcNow;
            var safeStart = (startDate ?? new DateTime(nowUtc.Year, nowUtc.Month, 1)).Date;
            var safeEnd = (endDate ?? nowUtc.Date).Date;
            if (safeEnd < safeStart)
            {
                (safeStart, safeEnd) = (safeEnd, safeStart);
            }

            var startUtc = DateTime.SpecifyKind(safeStart, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(safeEnd.AddDays(1), DateTimeKind.Utc);
            var selectedSite = string.IsNullOrWhiteSpace(site) ? null : site.Trim();
            var selectedCategory = string.IsNullOrWhiteSpace(failureCategory) ? null : failureCategory.Trim();

            var maintenanceBaseQuery = _context.Tickets
                .AsNoTracking()
                .Where(t =>
                    t.Department == "Mantenimiento" &&
                    t.CreatedDate >= startUtc &&
                    t.CreatedDate < endUtc);

            if (!string.IsNullOrWhiteSpace(selectedSite))
            {
                maintenanceBaseQuery = maintenanceBaseQuery.Where(t => t.Site == selectedSite);
            }

            if (!string.IsNullOrWhiteSpace(selectedCategory))
            {
                maintenanceBaseQuery = maintenanceBaseQuery.Where(t => t.FailureCategory == selectedCategory);
            }

            var tickets = await maintenanceBaseQuery
                .OrderBy(t => t.CreatedDate)
                .ToListAsync();

            var allMaintenanceQuery = _context.Tickets
                .AsNoTracking()
                .Where(t => t.Department == "Mantenimiento");

            var siteOptions = await allMaintenanceQuery
                .Where(t => t.Site != null && t.Site != "")
                .Select(t => t.Site.Trim())
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var failureCategoryOptions = await allMaintenanceQuery
                .Where(t => t.FailureCategory != null && t.FailureCategory != "")
                .Select(t => t.FailureCategory!.Trim())
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var preventiveTickets = tickets.Count(t =>
                !string.IsNullOrWhiteSpace(t.IncidentType) &&
                t.IncidentType.Contains("Preventiv", StringComparison.OrdinalIgnoreCase));

            var correctiveTickets = tickets.Count(t =>
                !string.IsNullOrWhiteSpace(t.IncidentType) &&
                (t.IncidentType.Contains("Correctiv", StringComparison.OrdinalIgnoreCase) ||
                 t.IncidentType.Contains("Reparac", StringComparison.OrdinalIgnoreCase)));

            var otherTickets = Math.Max(0, tickets.Count - preventiveTickets - correctiveTickets);

            var failureToFailureRows = BuildFailureToFailureRows(tickets);
            var kmValues = failureToFailureRows.Select(x => x.KmBetweenFailures).ToList();

            var topDamagedElements = tickets
                .Where(t => !string.IsNullOrWhiteSpace(t.DamagedElement))
                .GroupBy(t => new
                {
                    Category = string.IsNullOrWhiteSpace(t.FailureCategory) ? "Sin categoria" : t.FailureCategory!.Trim(),
                    Element = t.DamagedElement!.Trim()
                })
                .Select(g => new DamagedElementRow
                {
                    FailureCategory = g.Key.Category,
                    DamagedElement = g.Key.Element,
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.FailureCategory)
                .ThenBy(x => x.DamagedElement)
                .Take(20)
                .ToList();

            var failureCategorySummary = tickets
                .Where(t => !string.IsNullOrWhiteSpace(t.FailureCategory))
                .GroupBy(t => t.FailureCategory!.Trim())
                .Select(g => new FailureCategorySummaryRow
                {
                    FailureCategory = g.Key,
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.FailureCategory)
                .ToList();

            var maintenanceTypeSummary = tickets
                .GroupBy(t => NormalizeMaintenanceType(t.IncidentType))
                .Select(g => new MaintenanceTypeSummaryRow
                {
                    MaintenanceType = g.Key,
                    Total = g.Count(),
                    AverageResolutionHours = g
                        .Where(t => t.ClosedDate.HasValue)
                        .Select(t => (t.ClosedDate!.Value - t.CreatedDate).TotalHours)
                        .DefaultIfEmpty(0)
                        .Average()
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            return new MaintenanceReportsViewModel
            {
                StartDate = safeStart,
                EndDate = safeEnd,
                Site = selectedSite,
                FailureCategory = selectedCategory,
                SiteOptions = siteOptions,
                FailureCategoryOptions = failureCategoryOptions,
                TotalMaintenanceTickets = tickets.Count,
                PreventiveTickets = preventiveTickets,
                CorrectiveTickets = correctiveTickets,
                OtherTickets = otherTickets,
                FailureToFailureEvents = failureToFailureRows.Count,
                AverageKmBetweenFailures = kmValues.Count == 0 ? null : kmValues.Average(),
                MinKmBetweenFailures = kmValues.Count == 0 ? null : kmValues.Min(),
                MaxKmBetweenFailures = kmValues.Count == 0 ? null : kmValues.Max(),
                FailureToFailureRows = failureToFailureRows,
                TopDamagedElements = topDamagedElements,
                FailureCategorySummary = failureCategorySummary,
                MaintenanceTypeSummary = maintenanceTypeSummary
            };
        }

        private static List<FailureToFailureRow> BuildFailureToFailureRows(IReadOnlyList<Ticket> tickets)
        {
            var rows = new List<FailureToFailureRow>();

            var groupedByUnit = tickets
                .Where(t => !string.IsNullOrWhiteSpace(t.UnitCode))
                .GroupBy(t => t.UnitCode!.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var unitGroup in groupedByUnit)
            {
                Ticket? previousRepairedTicket = null;

                foreach (var current in unitGroup.OrderBy(t => t.CreatedDate))
                {
                    if (previousRepairedTicket != null &&
                        previousRepairedTicket.ExitOdometerKm.HasValue &&
                        current.FailureOdometerKm.HasValue)
                    {
                        var kmBetween = current.FailureOdometerKm.Value - previousRepairedTicket.ExitOdometerKm.Value;
                        if (kmBetween >= 0)
                        {
                            var previousRepairDate = previousRepairedTicket.ClosedDate ?? previousRepairedTicket.CreatedDate;
                            var daysBetween = (int)Math.Max(0, Math.Round((current.CreatedDate - previousRepairDate).TotalDays));

                            rows.Add(new FailureToFailureRow
                            {
                                UnitCode = unitGroup.Key,
                                PreviousTicketNumber = string.IsNullOrWhiteSpace(previousRepairedTicket.TicketNumber)
                                    ? $"TK-{previousRepairedTicket.Id:D4}"
                                    : previousRepairedTicket.TicketNumber!,
                                PreviousRepairDate = previousRepairDate,
                                PreviousExitKm = previousRepairedTicket.ExitOdometerKm.Value,
                                CurrentTicketNumber = string.IsNullOrWhiteSpace(current.TicketNumber)
                                    ? $"TK-{current.Id:D4}"
                                    : current.TicketNumber!,
                                CurrentFailureDate = current.CreatedDate,
                                CurrentFailureKm = current.FailureOdometerKm.Value,
                                KmBetweenFailures = kmBetween,
                                DaysBetweenFailures = daysBetween
                            });
                        }
                    }

                    if (current.ExitOdometerKm.HasValue)
                    {
                        previousRepairedTicket = current;
                    }
                }
            }

            return rows
                .OrderBy(x => x.KmBetweenFailures)
                .ThenByDescending(x => x.CurrentFailureDate)
                .ToList();
        }

        private static string NormalizeMaintenanceType(string? incidentType)
        {
            if (string.IsNullOrWhiteSpace(incidentType))
            {
                return "Sin tipo";
            }

            if (incidentType.Contains("Preventiv", StringComparison.OrdinalIgnoreCase))
            {
                return "Preventivo";
            }

            if (incidentType.Contains("Correctiv", StringComparison.OrdinalIgnoreCase) ||
                incidentType.Contains("Reparac", StringComparison.OrdinalIgnoreCase))
            {
                return "Correctivo";
            }

            return incidentType.Trim();
        }

        private static string EscapeCsv(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var escaped = raw.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        private async Task<string> GenerateNextNumberAsync(string prefix, IQueryable<string> existingNumbers)
        {
            var numberPrefix = $"{prefix}-{DateTime.UtcNow:yyyyMM}";
            var last = await existingNumbers
                .Where(x => EF.Functions.Like(x, numberPrefix + "-%"))
                .OrderByDescending(x => x)
                .FirstOrDefaultAsync();

            var next = 1;
            if (!string.IsNullOrWhiteSpace(last))
            {
                var index = last.LastIndexOf('-');
                if (index >= 0 && int.TryParse(last[(index + 1)..], out var parsed))
                {
                    next = parsed + 1;
                }
            }

            return $"{numberPrefix}-{next:D4}";
        }

        private string CurrentUserName()
        {
            return string.IsNullOrWhiteSpace(User?.Identity?.Name)
                ? "Sistema"
                : User.Identity!.Name!.Trim();
        }

        private static string? NullIfEmpty(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public sealed class TruckSketchInspectionSaveRequest
        {
            public string EquipmentCode { get; set; } = string.Empty;
            public string? DriverName { get; set; }
            public string? MechanicName { get; set; }
            public string? InspectionDate { get; set; }
            public List<TruckSketchInspectionRow>? Rows { get; set; }
        }

        public sealed class TruckSketchInspectionRow
        {
            public string Zone { get; set; } = string.Empty;
            public string Component { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Note { get; set; }
        }
    }
}
