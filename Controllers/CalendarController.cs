using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize]
    public class CalendarController : Controller
    {
        private const string AllValue = "all";
        private const string UnassignedValue = "unassigned";
        private static readonly CultureInfo EsCulture = CultureInfo.GetCultureInfo("es-ES");

        private readonly ApplicationDbContext _context;

        public CalendarController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? year, int? month, string? sla, string? technician)
        {
            var nowUtc = DateTime.UtcNow;
            var selectedYear = year ?? nowUtc.Year;
            var selectedMonth = month ?? nowUtc.Month;

            if (selectedMonth < 1 || selectedMonth > 12)
            {
                selectedMonth = nowUtc.Month;
            }

            if (selectedYear < 2000 || selectedYear > 2100)
            {
                selectedYear = nowUtc.Year;
            }

            var monthStart = new DateTime(selectedYear, selectedMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var activeTechnicians = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.Status != TicketStatus.Closed && !string.IsNullOrWhiteSpace(t.AssignedTechnician))
                .Select(t => t.AssignedTechnician!.Trim())
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            var selectedTechnician = NormalizeTechnician(technician, activeTechnicians);
            var selectedSla = NormalizeSla(sla);

            var monthTickets = await _context.Tickets
                .AsNoTracking()
                .Where(t =>
                    t.Status != TicketStatus.Closed &&
                    t.SLADeadline >= monthStart &&
                    t.SLADeadline < monthEnd)
                .Select(t => new CalendarTicketProjection
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber,
                    SLADeadline = t.SLADeadline,
                    AssignedTechnician = t.AssignedTechnician,
                    Priority = t.Priority
                })
                .ToListAsync();

            if (selectedTechnician == UnassignedValue)
            {
                monthTickets = monthTickets
                    .Where(t => string.IsNullOrWhiteSpace(t.AssignedTechnician))
                    .ToList();
            }
            else if (selectedTechnician != AllValue)
            {
                monthTickets = monthTickets
                    .Where(t => string.Equals(t.AssignedTechnician?.Trim(), selectedTechnician, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var classifiedTickets = monthTickets
                .Select(t => new
                {
                    Ticket = t,
                    SlaStatus = GetSlaStatus(t.SLADeadline, nowUtc)
                })
                .ToList();

            var filteredTickets = selectedSla == AllValue
                ? classifiedTickets
                : classifiedTickets
                    .Where(t => SlaMatches(t.SlaStatus, selectedSla))
                    .ToList();

            var model = new CalendarViewModel
            {
                Year = selectedYear,
                Month = selectedMonth,
                CurrentMonthLabel = BuildMonthLabel(monthStart),
                PreviousYear = monthStart.AddMonths(-1).Year,
                PreviousMonth = monthStart.AddMonths(-1).Month,
                NextYear = monthStart.AddMonths(1).Year,
                NextMonth = monthStart.AddMonths(1).Month,
                SelectedSla = selectedSla,
                SelectedTechnician = selectedTechnician,
                SlaCompliantCount = classifiedTickets.Count(t => t.SlaStatus == SlaStatus.Compliant),
                SlaNearDueCount = classifiedTickets.Count(t => t.SlaStatus == SlaStatus.NearDue),
                SlaBreachedCount = classifiedTickets.Count(t => t.SlaStatus == SlaStatus.Breached),
                CriticalPriorityCount = monthTickets.Count(t => t.Priority == PriorityLevel.Critical)
            };

            model.SlaOptions = new List<CalendarSelectOptionViewModel>
            {
                new() { Value = AllValue, Label = "Todos" },
                new() { Value = "compliant", Label = "SLA Cumplido" },
                new() { Value = "near", Label = "Próximo a Vencer" },
                new() { Value = "breached", Label = "SLA Vencido" }
            };

            model.TechnicianOptions = new List<CalendarSelectOptionViewModel>
            {
                new() { Value = AllValue, Label = "Todos" },
                new() { Value = UnassignedValue, Label = "Sin asignar" }
            };

            model.TechnicianOptions.AddRange(activeTechnicians.Select(t =>
                new CalendarSelectOptionViewModel
                {
                    Value = t,
                    Label = t
                }));

            var ticketsByDay = filteredTickets
                .GroupBy(t => t.Ticket.SLADeadline.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderBy(x => x.Ticket.SLADeadline)
                        .ThenBy(x => x.Ticket.TicketNumber)
                        .Select(x => CreateBadge(x.Ticket, x.SlaStatus))
                        .ToList());

            var offsetToMonday = ((int)monthStart.DayOfWeek + 6) % 7;
            var calendarStart = monthStart.Date.AddDays(-offsetToMonday);

            for (var i = 0; i < 42; i++)
            {
                var day = calendarStart.AddDays(i);

                model.Days.Add(new CalendarDayViewModel
                {
                    Date = day,
                    IsCurrentMonth = day.Month == selectedMonth,
                    IsToday = day == nowUtc.Date,
                    Tickets = ticketsByDay.TryGetValue(day, out var dayTickets)
                        ? dayTickets
                        : new List<CalendarTicketBadgeViewModel>()
                });
            }

            return View(model);
        }

        private static string NormalizeSla(string? sla)
        {
            if (string.IsNullOrWhiteSpace(sla))
            {
                return AllValue;
            }

            var normalized = sla.Trim().ToLowerInvariant();

            return normalized is AllValue or "compliant" or "near" or "breached"
                ? normalized
                : AllValue;
        }

        private static string NormalizeTechnician(string? technician, IReadOnlyCollection<string> activeTechnicians)
        {
            if (string.IsNullOrWhiteSpace(technician))
            {
                return AllValue;
            }

            var normalized = technician.Trim();

            if (string.Equals(normalized, AllValue, StringComparison.OrdinalIgnoreCase))
            {
                return AllValue;
            }

            if (string.Equals(normalized, UnassignedValue, StringComparison.OrdinalIgnoreCase))
            {
                return UnassignedValue;
            }

            var existing = activeTechnicians
                .FirstOrDefault(t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase));

            return existing ?? AllValue;
        }

        private static string BuildMonthLabel(DateTime monthStart)
        {
            var monthName = EsCulture.DateTimeFormat.GetMonthName(monthStart.Month);

            if (string.IsNullOrWhiteSpace(monthName))
            {
                monthName = monthStart.ToString("MMMM", EsCulture);
            }

            monthName = char.ToUpper(monthName[0], EsCulture) + monthName[1..];

            return $"{monthName} {monthStart.Year}";
        }

        private static bool SlaMatches(SlaStatus status, string selectedSla)
        {
            return selectedSla switch
            {
                "compliant" => status == SlaStatus.Compliant,
                "near" => status == SlaStatus.NearDue,
                "breached" => status == SlaStatus.Breached,
                _ => true
            };
        }

        private static SlaStatus GetSlaStatus(DateTime slaDeadlineUtc, DateTime nowUtc)
        {
            if (slaDeadlineUtc < nowUtc)
            {
                return SlaStatus.Breached;
            }

            if (slaDeadlineUtc <= nowUtc.AddHours(4))
            {
                return SlaStatus.NearDue;
            }

            return SlaStatus.Compliant;
        }

        private static CalendarTicketBadgeViewModel CreateBadge(CalendarTicketProjection ticket, SlaStatus slaStatus)
        {
            var cssClass = slaStatus switch
            {
                SlaStatus.Compliant => "ticket-chip ticket-chip--compliant",
                SlaStatus.NearDue => "ticket-chip ticket-chip--near",
                _ => "ticket-chip ticket-chip--breached"
            };

            var slaLabel = slaStatus switch
            {
                SlaStatus.Compliant => "SLA Cumplido",
                SlaStatus.NearDue => "Próximo a Vencer",
                _ => "SLA Vencido"
            };

            return new CalendarTicketBadgeViewModel
            {
                TicketId = ticket.Id,
                TicketNumber = string.IsNullOrWhiteSpace(ticket.TicketNumber)
                    ? $"TK-{ticket.Id:D4}"
                    : ticket.TicketNumber,
                SlaLabel = slaLabel,
                CssClass = cssClass,
                IsCritical = ticket.Priority == PriorityLevel.Critical
            };
        }

        private enum SlaStatus
        {
            Compliant,
            NearDue,
            Breached
        }

        private sealed class CalendarTicketProjection
        {
            public int Id { get; set; }
            public string? TicketNumber { get; set; }
            public DateTime SLADeadline { get; set; }
            public string? AssignedTechnician { get; set; }
            public PriorityLevel Priority { get; set; }
        }
    }
}
