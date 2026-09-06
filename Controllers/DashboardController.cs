using System;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            DateTime? fromDate,
            DateTime? toDate,
            TicketStatus? status,
            PriorityLevel? priority,
            string? site,
            string? department,
            string? technician)
        {
            var now = DateTime.UtcNow;
            var nearThreshold = now.AddHours(4);
            var normalizedSite = NormalizeOptional(site);
            var normalizedDepartment = NormalizeOptional(department);
            var normalizedTechnician = NormalizeOptional(technician);

            var model = new DashboardViewModel
            {
                FromDate = fromDate?.Date,
                ToDate = toDate?.Date,
                FilterStatus = status,
                FilterPriority = priority,
                FilterSite = normalizedSite,
                FilterDepartment = normalizedDepartment,
                FilterTechnician = normalizedTechnician
            };

            var ticketsBase = _context.Tickets
                .AsNoTracking()
                .AsQueryable();

            model.SiteOptions = await ticketsBase
                .Where(t => t.Site != null && t.Site != "")
                .Select(t => t.Site.Trim())
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();

            model.DepartmentOptions = await ticketsBase
                .Where(t => t.Department != null && t.Department != "")
                .Select(t => t.Department.Trim())
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();

            model.TechnicianOptions = await ticketsBase
                .Where(t => t.AssignedTechnician != null && t.AssignedTechnician != "")
                .Select(t => t.AssignedTechnician!.Trim())
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();

            var dimensionFilteredQuery = ApplyDimensionFilters(
                ticketsBase,
                model.FilterStatus,
                model.FilterPriority,
                model.FilterSite,
                model.FilterDepartment,
                model.FilterTechnician);

            var ticketsQuery = ApplyDateFilters(
                dimensionFilteredQuery,
                model.FromDate,
                model.ToDate);

            model.TotalTickets = await ticketsQuery.CountAsync();
            model.OpenTickets = await ticketsQuery.CountAsync(t => t.Status == TicketStatus.Open);
            model.InProgressTickets = await ticketsQuery.CountAsync(t => t.Status == TicketStatus.InProgress);
            model.ResolvedTickets = await ticketsQuery.CountAsync(t => t.Status == TicketStatus.Resolved);
            model.ClosedTickets = await ticketsQuery.CountAsync(t => t.Status == TicketStatus.Closed);
            model.ActiveTickets = await ticketsQuery.CountAsync(t => t.Status != TicketStatus.Closed);
            model.OverdueTickets = await ticketsQuery.CountAsync(t => t.Status != TicketStatus.Closed && t.SLADeadline < now);
            model.UnassignedActiveTickets = await ticketsQuery.CountAsync(t => t.Status != TicketStatus.Closed && (t.AssignedTechnician == null || t.AssignedTechnician == ""));
            model.CriticalActiveTickets = await ticketsQuery.CountAsync(t => t.Status != TicketStatus.Closed && t.Priority == PriorityLevel.Critical);

            model.TicketsByStatus = await ticketsQuery
                .GroupBy(t => t.Status)
                .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            model.TicketsByPriority = await ticketsQuery
                .GroupBy(t => t.Priority)
                .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            model.TicketsBySite = await ticketsQuery
                .GroupBy(t => t.Site == null || t.Site == "" ? "Sin definir" : t.Site)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            model.TicketsByDepartment = await ticketsQuery
                .GroupBy(t => t.Department == null || t.Department == "" ? "Sin definir" : t.Department)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            model.TicketsByType = await ticketsQuery
                .GroupBy(t => t.IncidentType == null || t.IncidentType == "" ? "Sin definir" : t.IncidentType)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            var activeQuery = ticketsQuery.Where(t => t.Status != TicketStatus.Closed);
            var activeCount = await activeQuery.CountAsync();

            model.SlaCompliant = await activeQuery
                .CountAsync(t => t.SLADeadline >= nearThreshold);

            model.SlaNearDue = await activeQuery
                .CountAsync(t => t.SLADeadline < nearThreshold && t.SLADeadline >= now);

            model.SlaBreached = await activeQuery
                .CountAsync(t => t.SLADeadline < now);

            if (activeCount > 0)
            {
                model.SlaCompliantPercentage = Math.Round((double)model.SlaCompliant / activeCount * 100, 1);
                model.SlaNearDuePercentage = Math.Round((double)model.SlaNearDue / activeCount * 100, 1);
                model.SlaBreachedPercentage = Math.Round((double)model.SlaBreached / activeCount * 100, 1);
            }

            model.SlaComplianceRate = model.SlaCompliantPercentage;

            var mttrMinutes = await ticketsQuery
                .Where(t => t.ClosedDate.HasValue)
                .Select(t => (double?)EF.Functions.DateDiffMinute(t.CreatedDate, t.ClosedDate!.Value))
                .AverageAsync() ?? 0;

            model.MttrHours = Math.Round(mttrMinutes / 60d, 2);

            var activeCreatedDates = await activeQuery
                .Select(t => t.CreatedDate)
                .ToListAsync();

            model.BacklogAgingBuckets = new()
            {
                ["0-2 dias"] = activeCreatedDates.Count(d => (now - d).TotalDays <= 2),
                ["3-7 dias"] = activeCreatedDates.Count(d =>
                {
                    var days = (now - d).TotalDays;
                    return days > 2 && days <= 7;
                }),
                ["8+ dias"] = activeCreatedDates.Count(d => (now - d).TotalDays > 7)
            };

            var periodEndExclusive = model.ToDate?.Date.AddDays(1) ?? now;
            var periodStart = model.FromDate?.Date ?? periodEndExclusive.AddDays(-30);
            if (periodStart >= periodEndExclusive)
            {
                periodStart = periodEndExclusive.AddDays(-1);
            }

            var periodDays = Math.Max(1, (int)Math.Ceiling((periodEndExclusive - periodStart).TotalDays));
            var previousPeriodStart = periodStart.AddDays(-periodDays);
            var previousPeriodEnd = periodStart;

            model.TicketsCreatedCurrentPeriod = await dimensionFilteredQuery
                .CountAsync(t => t.CreatedDate >= periodStart && t.CreatedDate < periodEndExclusive);

            model.TicketsCreatedPreviousPeriod = await dimensionFilteredQuery
                .CountAsync(t => t.CreatedDate >= previousPeriodStart && t.CreatedDate < previousPeriodEnd);

            model.CreatedTrendPercentage = model.TicketsCreatedPreviousPeriod == 0
                ? (model.TicketsCreatedCurrentPeriod > 0 ? 100 : 0)
                : Math.Round(
                    ((double)(model.TicketsCreatedCurrentPeriod - model.TicketsCreatedPreviousPeriod) / model.TicketsCreatedPreviousPeriod) * 100,
                    1);

            var currentYear = now.Year;
            var previousYear = currentYear - 1;

            model.CurrentYear = currentYear;

            model.TicketsByMonthCurrent = await dimensionFilteredQuery
                .Where(t => t.CreatedDate.Year == currentYear)
                .GroupBy(t => t.CreatedDate.Month)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            model.TicketsByMonthPrevious = await dimensionFilteredQuery
                .Where(t => t.CreatedDate.Year == previousYear)
                .GroupBy(t => t.CreatedDate.Month)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            var currentTotalYear = model.TicketsByMonthCurrent.Values.Sum();
            var previousTotalYear = model.TicketsByMonthPrevious.Values.Sum();

            model.YearGrowthPercentage = previousTotalYear == 0
                ? (currentTotalYear > 0 ? 100 : 0)
                : Math.Round(((double)(currentTotalYear - previousTotalYear) / previousTotalYear) * 100, 2);

            model.AtRiskTickets = await activeQuery
                .Where(t => t.SLADeadline <= nearThreshold)
                .OrderBy(t => t.SLADeadline)
                .Take(12)
                .Select(t => new DashboardRiskTicketItem
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber ?? ("TK-" + t.Id),
                    Department = t.Department ?? "Sin definir",
                    Site = t.Site ?? "Sin definir",
                    Priority = t.Priority,
                    Status = t.Status,
                    AssignedTechnician = t.AssignedTechnician == null || t.AssignedTechnician == ""
                        ? "Sin asignar"
                        : t.AssignedTechnician,
                    SlaDeadline = t.SLADeadline,
                    HoursToSla = EF.Functions.DateDiffMinute(now, t.SLADeadline) / 60.0
                })
                .ToListAsync();

            model.TechnicianLoads = await activeQuery
                .GroupBy(t => t.AssignedTechnician == null || t.AssignedTechnician == "" ? "Sin asignar" : t.AssignedTechnician)
                .Select(g => new DashboardTechnicianLoadItem
                {
                    Technician = g.Key,
                    ActiveTickets = g.Count(),
                    CriticalTickets = g.Count(t => t.Priority == PriorityLevel.Critical),
                    OverdueTickets = g.Count(t => t.SLADeadline < now)
                })
                .OrderByDescending(x => x.ActiveTickets)
                .ThenByDescending(x => x.OverdueTickets)
                .Take(8)
                .ToListAsync();

            model.RecentTickets = await ticketsQuery
                .OrderByDescending(t => t.CreatedDate)
                .Take(10)
                .ToListAsync();

            return View(model);
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static IQueryable<Ticket> ApplyDimensionFilters(
            IQueryable<Ticket> query,
            TicketStatus? status,
            PriorityLevel? priority,
            string? site,
            string? department,
            string? technician)
        {
            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (priority.HasValue)
            {
                query = query.Where(t => t.Priority == priority.Value);
            }

            if (!string.IsNullOrWhiteSpace(site))
            {
                query = query.Where(t => t.Site == site);
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(t => t.Department == department);
            }

            if (!string.IsNullOrWhiteSpace(technician))
            {
                query = query.Where(t => (t.AssignedTechnician ?? "") == technician);
            }

            return query;
        }

        private static IQueryable<Ticket> ApplyDateFilters(
            IQueryable<Ticket> query,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (fromDate.HasValue)
            {
                query = query.Where(t => t.CreatedDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var endExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(t => t.CreatedDate < endExclusive);
            }

            return query;
        }
    }
}
