using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.Controllers
{
    // 🔐 Requiere usuario autenticado
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var nearThreshold = now.AddHours(4);

            var model = new DashboardViewModel();

            var ticketsQuery = _context.Tickets.AsQueryable();

            // =============================
            // KPI PRINCIPALES
            // =============================

            model.TotalTickets = await ticketsQuery.CountAsync();

            model.OpenTickets = await ticketsQuery
                .CountAsync(t => t.Status == TicketStatus.Open);

            model.InProgressTickets = await ticketsQuery
                .CountAsync(t => t.Status == TicketStatus.InProgress);

            model.ClosedTickets = await ticketsQuery
                .CountAsync(t => t.Status == TicketStatus.Closed);

            model.OverdueTickets = await ticketsQuery
                .CountAsync(t =>
                    t.Status != TicketStatus.Closed &&
                    t.SLADeadline < now);

            // =============================
            // DISTRIBUCIONES
            // =============================

            model.TicketsBySite = await ticketsQuery
                .GroupBy(t => t.Site ?? "Sin definir")
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            model.TicketsByDepartment = await ticketsQuery
                .GroupBy(t => t.Department ?? "Sin definir")
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            model.TicketsByType = await ticketsQuery
                .GroupBy(t => t.IncidentType ?? "Sin definir")
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            // =============================
            // SLA
            // =============================

            var activeQuery = ticketsQuery
                .Where(t => t.Status != TicketStatus.Closed);

            var activeCount = await activeQuery.CountAsync();

            model.SlaCompliant = await activeQuery
                .CountAsync(t => t.SLADeadline >= nearThreshold);

            model.SlaNearDue = await activeQuery
                .CountAsync(t =>
                    t.SLADeadline < nearThreshold &&
                    t.SLADeadline >= now);

            model.SlaBreached = await activeQuery
                .CountAsync(t => t.SLADeadline < now);

            if (activeCount > 0)
            {
                model.SlaCompliantPercentage =
                    Math.Round((double)model.SlaCompliant / activeCount * 100, 0);

                model.SlaNearDuePercentage =
                    Math.Round((double)model.SlaNearDue / activeCount * 100, 0);

                model.SlaBreachedPercentage =
                    Math.Round((double)model.SlaBreached / activeCount * 100, 0);
            }

            // =============================
            // COMPARACIÓN ANUAL
            // =============================

            var currentYear = now.Year;
            var previousYear = currentYear - 1;

            model.CurrentYear = currentYear;

            model.TicketsByMonthCurrent = await ticketsQuery
                .Where(t => t.CreatedDate.Year == currentYear)
                .GroupBy(t => t.CreatedDate.Month)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            model.TicketsByMonthPrevious = await ticketsQuery
                .Where(t => t.CreatedDate.Year == previousYear)
                .GroupBy(t => t.CreatedDate.Month)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            var currentTotal = await ticketsQuery
                .CountAsync(t => t.CreatedDate.Year == currentYear);

            var previousTotal = await ticketsQuery
                .CountAsync(t => t.CreatedDate.Year == previousYear);

            model.YearGrowthPercentage = previousTotal == 0
                ? 100
                : Math.Round(((double)(currentTotal - previousTotal) / previousTotal) * 100, 2);

            // =============================
            // ÚLTIMOS TICKETS
            // =============================

            model.RecentTickets = await ticketsQuery
                .OrderByDescending(t => t.CreatedDate)
                .Take(6)
                .ToListAsync();

            return View(model);
        }
    }
}
