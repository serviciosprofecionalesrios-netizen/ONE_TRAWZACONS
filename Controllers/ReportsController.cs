using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class ReportsController : Controller
    {
        private const string AllValue = "all";
        private static readonly string[] AllowedTabs = { "summary", "technician", "area", "site" };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ReportsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? startDate,
            DateTime? endDate,
            string? technician,
            string? area,
            string? site,
            string? tab,
            bool printMode = false)
        {
            var report = await BuildReportAsync(startDate, endDate, technician, area, site, tab, printMode);
            return View(report.Model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            DateTime? startDate,
            DateTime? endDate,
            string? technician,
            string? area,
            string? site,
            string? tab)
        {
            var report = await BuildReportAsync(startDate, endDate, technician, area, site, tab, printMode: false);

            var csv = new StringBuilder();
            csv.AppendLine("Ticket,Fecha Creacion,Fecha Cierre,Estado,Prioridad,Tecnico,Area,Sitio,Tipo,SLA");

            foreach (var t in report.Tickets.OrderByDescending(x => x.CreatedDate))
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(t.TicketNumber),
                    EscapeCsv(t.CreatedDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                    EscapeCsv(t.ClosedDate?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty),
                    EscapeCsv(t.Status.ToString()),
                    EscapeCsv(t.Priority.ToString()),
                    EscapeCsv(LabelOrDefault(t.AssignedTechnician, "Sin asignar")),
                    EscapeCsv(LabelOrDefault(t.Department, "Sin definir")),
                    EscapeCsv(LabelOrDefault(t.Site, "Sin definir")),
                    EscapeCsv(LabelOrDefault(t.IncidentType, "Otros")),
                    EscapeCsv(t.SLADeadline.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))));
            }

            var fileName = $"ReporteTickets_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());

            return File(bytes, "text/csv", fileName);
        }


        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            DateTime? startDate,
            DateTime? endDate,
            string? technician,
            string? area,
            string? site,
            string? tab)
        {
            var report = await BuildReportAsync(startDate, endDate, technician, area, site, tab, printMode: false);

            var rows = report.Tickets
                .Select(t => new ReportsPdfTicketRow(
                    TicketNumber: string.IsNullOrWhiteSpace(t.TicketNumber) ? $"TK-{t.Id:D4}" : t.TicketNumber,
                    CreatedDate: t.CreatedDate,
                    StatusLabel: t.Status switch
                    {
                        TicketStatus.Open => "Abierto",
                        TicketStatus.InProgress => "En Progreso",
                        TicketStatus.Resolved => "Resuelto",
                        TicketStatus.Closed => "Cerrado",
                        _ => t.Status.ToString()
                    },
                    PriorityLabel: t.Priority switch
                    {
                        PriorityLevel.Low => "Baja",
                        PriorityLevel.Medium => "Media",
                        PriorityLevel.High => "Alta",
                        PriorityLevel.Critical => "Critica",
                        _ => t.Priority.ToString()
                    },
                    Technician: LabelOrDefault(t.AssignedTechnician, "Sin asignar"),
                    Area: LabelOrDefault(t.Department, "Sin definir"),
                    Site: LabelOrDefault(t.Site, "Sin definir")))
                .ToList();

            var bytes = ReportsPdfReportService.GenerateReportPdf(report.Model, rows, _environment.WebRootPath);
            var fileName = $"ReporteTickets_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return File(bytes, "application/pdf", fileName);
        }
        private async Task<(ReportIndexViewModel Model, List<ReportTicketRow> Tickets)> BuildReportAsync(
            DateTime? startDate,
            DateTime? endDate,
            string? technician,
            string? area,
            string? site,
            string? tab,
            bool printMode)
        {
            var utcNow = DateTime.UtcNow;

            var safeStartDate = (startDate ?? new DateTime(utcNow.Year, utcNow.Month, 1)).Date;
            var safeEndDate = (endDate ?? utcNow.Date).Date;

            if (safeEndDate < safeStartDate)
            {
                (safeStartDate, safeEndDate) = (safeEndDate, safeStartDate);
            }

            var normalizedTechnician = NormalizeFilter(technician);
            var normalizedArea = NormalizeFilter(area);
            var normalizedSite = NormalizeFilter(site);

            var selectedTab = NormalizeTab(tab);

            var startInclusiveUtc = DateTime.SpecifyKind(safeStartDate, DateTimeKind.Utc);
            var endExclusiveUtc = DateTime.SpecifyKind(safeEndDate.AddDays(1), DateTimeKind.Utc);

            var ticketsQuery = _context.Tickets
                .AsNoTracking()
                .Where(t => t.CreatedDate >= startInclusiveUtc && t.CreatedDate < endExclusiveUtc);

            if (normalizedTechnician != AllValue)
            {
                if (normalizedTechnician == "unassigned")
                {
                    ticketsQuery = ticketsQuery.Where(t => string.IsNullOrWhiteSpace(t.AssignedTechnician));
                }
                else
                {
                    ticketsQuery = ticketsQuery.Where(t => t.AssignedTechnician == normalizedTechnician);
                }
            }

            if (normalizedArea != AllValue)
            {
                ticketsQuery = ticketsQuery.Where(t => t.Department == normalizedArea);
            }

            if (normalizedSite != AllValue)
            {
                ticketsQuery = ticketsQuery.Where(t => t.Site == normalizedSite);
            }

            var tickets = await ticketsQuery
                .Select(t => new ReportTicketRow
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber ?? string.Empty,
                    CreatedDate = t.CreatedDate,
                    ClosedDate = t.ClosedDate,
                    Status = t.Status,
                    Priority = t.Priority,
                    AssignedTechnician = t.AssignedTechnician,
                    Department = t.Department,
                    Site = t.Site,
                    IncidentType = t.IncidentType,
                    SLADeadline = t.SLADeadline
                })
                .ToListAsync();

            var totalTickets = tickets.Count;
            var resolvedTickets = tickets.Count(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed);
            var inProgressTickets = tickets.Count(t => t.Status == TicketStatus.InProgress);
            var pendingTickets = tickets.Count(t => t.Status == TicketStatus.Open);
            var overdueTickets = tickets.Count(t => t.Status != TicketStatus.Closed && t.SLADeadline < utcNow);

            var closedForAverage = tickets
                .Where(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed)
                .Where(t => t.ClosedDate.HasValue)
                .ToList();

            var avgResolutionHours = closedForAverage.Any()
                ? Math.Round(closedForAverage.Average(t => (t.ClosedDate!.Value - t.CreatedDate).TotalHours), 1)
                : 0;

            var slaCompliantCount = tickets.Count(t => IsSlaCompliant(t, utcNow));
            var slaCompliancePercent = totalTickets > 0
                ? (int)Math.Round((double)slaCompliantCount * 100 / totalTickets, MidpointRounding.AwayFromZero)
                : 0;

            var resolvedRate = totalTickets > 0
                ? (double)resolvedTickets * 100 / totalTickets
                : 0;

            var satisfaction = (int)Math.Round((resolvedRate * 0.7) + (slaCompliancePercent * 0.3), MidpointRounding.AwayFromZero);
            satisfaction = Math.Clamp(satisfaction, 0, 100);

            var distributionByType = BuildGroupData(
                tickets,
                t => LabelOrDefault(t.IncidentType, "Otros"),
                totalTickets);

            var byTechnician = BuildGroupData(
                tickets,
                t => LabelOrDefault(t.AssignedTechnician, "Sin asignar"),
                totalTickets);

            var byArea = BuildGroupData(
                tickets,
                t => LabelOrDefault(t.Department, "Sin definir"),
                totalTickets);

            var bySite = BuildGroupData(
                tickets,
                t => LabelOrDefault(t.Site, "Sin definir"),
                totalTickets);

            var model = new ReportIndexViewModel
            {
                StartDate = safeStartDate,
                EndDate = safeEndDate,
                SelectedTechnician = normalizedTechnician,
                SelectedArea = normalizedArea,
                SelectedSite = normalizedSite,
                ActiveTab = selectedTab,
                PrintMode = printMode,
                TotalTickets = totalTickets,
                ResolvedTickets = resolvedTickets,
                InProgressTickets = inProgressTickets,
                PendingTickets = pendingTickets,
                OverdueTickets = overdueTickets,
                AverageResolutionHours = avgResolutionHours,
                SatisfactionPercent = satisfaction,
                SlaCompliancePercent = slaCompliancePercent,
                IncidentTypeDistribution = distributionByType,
                TicketsByTechnician = byTechnician,
                TicketsByArea = byArea,
                TicketsBySite = bySite,
                TechnicianOptions = await GetDistinctOptionsAsync(t => t.AssignedTechnician, includeUnassigned: true),
                AreaOptions = await GetDistinctOptionsAsync(t => t.Department),
                SiteOptions = await GetDistinctOptionsAsync(t => t.Site)
            };

            return (model, tickets);
        }

        private async Task<List<ReportSelectOptionViewModel>> GetDistinctOptionsAsync(
            System.Linq.Expressions.Expression<Func<Ticket, string?>> selector,
            bool includeUnassigned = false)
        {
            var values = await _context.Tickets
                .AsNoTracking()
                .Select(selector)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();

            var options = new List<ReportSelectOptionViewModel>
            {
                new() { Value = AllValue, Label = "Todos" }
            };

            if (includeUnassigned)
            {
                options.Add(new ReportSelectOptionViewModel
                {
                    Value = "unassigned",
                    Label = "Sin asignar"
                });
            }

            options.AddRange(values.Select(v => new ReportSelectOptionViewModel
            {
                Value = v,
                Label = v
            }));

            return options;
        }

        private static List<ReportGroupItemViewModel> BuildGroupData(
            IEnumerable<ReportTicketRow> tickets,
            Func<ReportTicketRow, string> keySelector,
            int total)
        {
            return tickets
                .GroupBy(keySelector)
                .Select(g => new ReportGroupItemViewModel
                {
                    Label = g.Key,
                    Count = g.Count(),
                    Percentage = total > 0
                        ? Math.Round((double)g.Count() * 100 / total, 1)
                        : 0
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Label)
                .ToList();
        }

        private static bool IsSlaCompliant(ReportTicketRow ticket, DateTime utcNow)
        {
            if (ticket.Status == TicketStatus.Closed && ticket.ClosedDate.HasValue)
            {
                return ticket.ClosedDate.Value <= ticket.SLADeadline;
            }

            return utcNow <= ticket.SLADeadline;
        }

        private static string NormalizeTab(string? tab)
        {
            if (string.IsNullOrWhiteSpace(tab))
            {
                return "summary";
            }

            var normalized = tab.Trim().ToLowerInvariant();
            return AllowedTabs.Contains(normalized) ? normalized : "summary";
        }

        private static string NormalizeFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AllValue;
            }

            return value.Trim();
        }

        private static string LabelOrDefault(string? value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value)
                ? defaultValue
                : value.Trim();
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private sealed class ReportTicketRow
        {
            public int Id { get; set; }
            public string TicketNumber { get; set; } = string.Empty;
            public DateTime CreatedDate { get; set; }
            public DateTime? ClosedDate { get; set; }
            public TicketStatus Status { get; set; }
            public PriorityLevel Priority { get; set; }
            public string? AssignedTechnician { get; set; }
            public string? Department { get; set; }
            public string? Site { get; set; }
            public string? IncidentType { get; set; }
            public DateTime SLADeadline { get; set; }
        }
    }
}




