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
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class CalendarController : Controller
    {
        private const string AllValue = "all";
        private const string UnassignedValue = "unassigned";
        private const string QuickToday = "today";
        private const string QuickOverdue = "overdue";
        private const string QuickCritical = "critical";
        private const string QuickMyLoad = "myload";
        private const string ScopeMaintenance = "maintenance";
        private const string ScopeIt = "it";
        private const string MaintenanceViewAppointments = "appointments";
        private const string MaintenanceViewEquipment = "equipment";
        private static readonly CultureInfo EsCulture = CultureInfo.GetCultureInfo("es-ES");
        private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

        private readonly ApplicationDbContext _context;

        public CalendarController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? year, int? month, string? sla, string? technician, string? quick, string? scope, string? selectedDate)
        {
            var normalizedScope = NormalizeScope(scope);
            if (normalizedScope == ScopeMaintenance)
            {
                return await BuildMaintenanceCalendarAsync(year, month, sla, technician, normalizedScope, selectedDate);
            }

            var nowUtc = DateTime.UtcNow;
            var currentUser = User.Identity?.Name?.Trim();
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
            var selectedQuick = NormalizeQuick(quick);

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

            monthTickets = ApplyQuickFilter(monthTickets, selectedQuick, nowUtc, currentUser);

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
                Scope = ScopeIt,
                IsMaintenanceScope = false,
                Year = selectedYear,
                Month = selectedMonth,
                CurrentMonthLabel = BuildMonthLabel(monthStart),
                PreviousYear = monthStart.AddMonths(-1).Year,
                PreviousMonth = monthStart.AddMonths(-1).Month,
                NextYear = monthStart.AddMonths(1).Year,
                NextMonth = monthStart.AddMonths(1).Month,
                SelectedSla = selectedSla,
                SelectedTechnician = selectedTechnician,
                SelectedQuick = selectedQuick,
                TotalTicketsCount = monthTickets.Count,
                SlaCompliantCount = classifiedTickets.Count(t => t.SlaStatus == SlaStatus.Compliant),
                SlaNearDueCount = classifiedTickets.Count(t => t.SlaStatus == SlaStatus.NearDue),
                SlaBreachedCount = classifiedTickets.Count(t => t.SlaStatus == SlaStatus.Breached),
                UnassignedCount = monthTickets.Count(t => string.IsNullOrWhiteSpace(t.AssignedTechnician)),
                CriticalPriorityCount = monthTickets.Count(t => t.Priority == PriorityLevel.Critical),
                MyLoadCount = string.IsNullOrWhiteSpace(currentUser)
                    ? 0
                    : monthTickets.Count(t =>
                        !string.IsNullOrWhiteSpace(t.AssignedTechnician) &&
                        string.Equals(t.AssignedTechnician.Trim(), currentUser, StringComparison.OrdinalIgnoreCase))
            };

            model.SlaOptions = new List<CalendarSelectOptionViewModel>
            {
                new() { Value = AllValue, Label = "Todos" },
                new() { Value = "compliant", Label = "SLA Cumplido" },
                new() { Value = "near", Label = "Proximo a Vencer" },
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

            model.TechnicianLoads = classifiedTickets
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Ticket.AssignedTechnician)
                    ? "Sin asignar"
                    : t.Ticket.AssignedTechnician!.Trim())
                .Select(g => new CalendarTechnicianLoadViewModel
                {
                    Technician = g.Key,
                    TotalTickets = g.Count(),
                    BreachedTickets = g.Count(x => x.SlaStatus == SlaStatus.Breached),
                    CriticalTickets = g.Count(x => x.Ticket.Priority == PriorityLevel.Critical)
                })
                .OrderByDescending(x => x.TotalTickets)
                .ThenByDescending(x => x.BreachedTickets)
                .ThenBy(x => x.Technician)
                .Take(8)
                .ToList();

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

        private async Task<IActionResult> BuildMaintenanceCalendarAsync(
            int? year,
            int? month,
            string? sla,
            string? technician,
            string normalizedScope,
            string? selectedDate)
        {
            var nowUtc = DateTime.UtcNow;
            var nowLocal = ToBusinessLocalTime(nowUtc);
            var currentUser = User.Identity?.Name?.Trim();

            var selectedYear = year ?? nowLocal.Year;
            var selectedMonth = month ?? nowLocal.Month;
            if (selectedMonth < 1 || selectedMonth > 12)
            {
                selectedMonth = nowLocal.Month;
            }

            if (selectedYear < 2000 || selectedYear > 2100)
            {
                selectedYear = nowLocal.Year;
            }

            var monthStart = new DateTime(selectedYear, selectedMonth, 1);
            var monthEnd = monthStart.AddMonths(1);
            var offsetToMonday = ((int)monthStart.DayOfWeek + 6) % 7;
            var calendarStart = monthStart.Date.AddDays(-offsetToMonday);
            var calendarEnd = calendarStart.AddDays(42);

            var selectedDateLocal = ParseSelectedDate(selectedDate) ??
                                    (nowLocal.Month == selectedMonth && nowLocal.Year == selectedYear
                                        ? nowLocal.Date
                                        : monthStart.Date);

            var maintenanceView = NormalizeMaintenanceView(sla);

            var ticketTechnicians = await _context.Tickets
                .AsNoTracking()
                .Where(t =>
                    t.Department == "Mantenimiento" &&
                    t.Status != TicketStatus.Closed &&
                    !string.IsNullOrWhiteSpace(t.AssignedTechnician))
                .Select(t => t.AssignedTechnician!.Trim())
                .ToListAsync();

            var appointmentTechnicians = await _context.MaintenanceAppointments
                .AsNoTracking()
                .Where(a => !string.IsNullOrWhiteSpace(a.AssignedTechnician) && a.Status != "Cancelada")
                .Select(a => a.AssignedTechnician!.Trim())
                .ToListAsync();

            var activeTechnicians = ticketTechnicians
                .Concat(appointmentTechnicians)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            var selectedTechnician = NormalizeTechnician(technician, activeTechnicians);

            var appointments = await _context.MaintenanceAppointments
                .AsNoTracking()
                .Where(a => a.ScheduledFor >= calendarStart && a.ScheduledFor < calendarEnd)
                .Select(a => new MaintenanceAppointmentProjection
                {
                    Id = a.Id,
                    AppointmentNumber = a.AppointmentNumber,
                    ScheduledFor = a.ScheduledFor,
                    Site = a.Site,
                    AssetOrArea = a.AssetOrArea,
                    AssignedTechnician = a.AssignedTechnician,
                    Status = a.Status
                })
                .ToListAsync();

            var rangeStartUtc = TimeZoneInfo.ConvertTimeToUtc(calendarStart, BusinessTimeZone);
            var rangeEndUtc = TimeZoneInfo.ConvertTimeToUtc(calendarEnd, BusinessTimeZone);

            var maintenanceTickets = await _context.Tickets
                .AsNoTracking()
                .Where(t =>
                    t.Department != null &&
                    t.Department.Trim() == "Mantenimiento" &&
                    t.CreatedDate < rangeEndUtc &&
                    (!t.ClosedDate.HasValue || t.ClosedDate.Value >= rangeStartUtc))
                .Select(t => new MaintenanceEquipmentProjection
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber,
                    UnitCode = t.UnitCode,
                    Site = t.Site,
                    AssignedTechnician = t.AssignedTechnician,
                    Status = t.Status,
                    CreatedDateUtc = t.CreatedDate,
                    ClosedDateUtc = t.ClosedDate,
                    IncidentType = t.IncidentType,
                    DamagedElement = t.DamagedElement,
                    Priority = t.Priority,
                    SlaDeadline = t.SLADeadline
                })
                .ToListAsync();

            if (selectedTechnician == UnassignedValue)
            {
                appointments = appointments.Where(a => string.IsNullOrWhiteSpace(a.AssignedTechnician)).ToList();
                maintenanceTickets = maintenanceTickets.Where(t => string.IsNullOrWhiteSpace(t.AssignedTechnician)).ToList();
            }
            else if (selectedTechnician != AllValue)
            {
                appointments = appointments
                    .Where(a => string.Equals(a.AssignedTechnician?.Trim(), selectedTechnician, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                maintenanceTickets = maintenanceTickets
                    .Where(t => string.Equals(t.AssignedTechnician?.Trim(), selectedTechnician, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var ticket in maintenanceTickets)
            {
                ticket.CreatedLocal = ToBusinessLocalTime(ticket.CreatedDateUtc);
                ticket.ClosedLocal = ticket.ClosedDateUtc.HasValue ? ToBusinessLocalTime(ticket.ClosedDateUtc.Value) : null;
            }

            var monthAppointmentsCount = appointments.Count(a => a.ScheduledFor >= monthStart && a.ScheduledFor < monthEnd);
            var activeEquipmentNow = maintenanceTickets.Where(t => IsEquipmentActiveOnDate(t, nowLocal.Date)).ToList();
            var enteredThisMonth = maintenanceTickets.Count(t => t.CreatedLocal.Date >= monthStart.Date && t.CreatedLocal.Date < monthEnd.Date);
            var closedThisMonth = maintenanceTickets.Count(t => t.ClosedLocal.HasValue && t.ClosedLocal.Value.Date >= monthStart.Date && t.ClosedLocal.Value.Date < monthEnd.Date);

            var equipmentFromSelectedDate = maintenanceTickets
                .Where(t => IsEquipmentActiveOnDate(t, selectedDateLocal.Date))
                .OrderBy(t => t.CreatedLocal)
                .ThenBy(t => t.TicketNumber)
                .ToList();

            var selectedDateAppointments = appointments
                .Where(a => a.ScheduledFor.Date == selectedDateLocal.Date)
                .OrderBy(a => a.ScheduledFor)
                .ToList();

            var model = new CalendarViewModel
            {
                Scope = normalizedScope,
                IsMaintenanceScope = true,
                Year = selectedYear,
                Month = selectedMonth,
                CurrentMonthLabel = BuildMonthLabel(monthStart),
                PreviousYear = monthStart.AddMonths(-1).Year,
                PreviousMonth = monthStart.AddMonths(-1).Month,
                NextYear = monthStart.AddMonths(1).Year,
                NextMonth = monthStart.AddMonths(1).Month,
                SelectedSla = maintenanceView,
                SelectedTechnician = selectedTechnician,
                SelectedQuick = AllValue,
                SelectedDate = selectedDateLocal.Date,
                SelectedDateIso = selectedDateLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                MaintenanceAppointmentsCount = monthAppointmentsCount,
                MaintenanceActiveEquipmentCount = activeEquipmentNow.Count,
                MaintenanceEnteredThisMonthCount = enteredThisMonth,
                MaintenanceClosedThisMonthCount = closedThisMonth,
                MaintenanceInWorkshopFromDateCount = equipmentFromSelectedDate.Count,
                TotalTicketsCount = maintenanceView switch
                {
                    MaintenanceViewAppointments => monthAppointmentsCount,
                    MaintenanceViewEquipment => activeEquipmentNow.Count,
                    _ => monthAppointmentsCount + activeEquipmentNow.Count
                },
                UnassignedCount = activeEquipmentNow.Count(t => string.IsNullOrWhiteSpace(t.AssignedTechnician)),
                CriticalPriorityCount = activeEquipmentNow.Count(t => t.Priority == PriorityLevel.Critical),
                MyLoadCount = string.IsNullOrWhiteSpace(currentUser)
                    ? 0
                    : activeEquipmentNow.Count(t =>
                        !string.IsNullOrWhiteSpace(t.AssignedTechnician) &&
                        string.Equals(t.AssignedTechnician.Trim(), currentUser, StringComparison.OrdinalIgnoreCase))
            };

            model.SlaOptions = new List<CalendarSelectOptionViewModel>
            {
                new() { Value = AllValue, Label = "Todos" },
                new() { Value = MaintenanceViewAppointments, Label = "Citas" },
                new() { Value = MaintenanceViewEquipment, Label = "Equipos" }
            };

            model.TechnicianOptions = new List<CalendarSelectOptionViewModel>
            {
                new() { Value = AllValue, Label = "Todos" },
                new() { Value = UnassignedValue, Label = "Sin asignar" }
            };
            model.TechnicianOptions.AddRange(activeTechnicians.Select(t => new CalendarSelectOptionViewModel { Value = t, Label = t }));

            model.TechnicianLoads = activeEquipmentNow
                .GroupBy(t => string.IsNullOrWhiteSpace(t.AssignedTechnician) ? "Sin asignar" : t.AssignedTechnician!.Trim())
                .Select(g => new CalendarTechnicianLoadViewModel
                {
                    Technician = g.Key,
                    TotalTickets = g.Count(),
                    BreachedTickets = g.Count(x => x.SlaDeadline < nowUtc),
                    CriticalTickets = g.Count(x => x.Priority == PriorityLevel.Critical)
                })
                .OrderByDescending(x => x.TotalTickets)
                .ThenByDescending(x => x.BreachedTickets)
                .ThenBy(x => x.Technician)
                .Take(8)
                .ToList();

            for (var i = 0; i < 42; i++)
            {
                var day = calendarStart.AddDays(i);
                var dayAppointments = appointments.Where(a => a.ScheduledFor.Date == day.Date).OrderBy(a => a.ScheduledFor).ToList();
                var dayEquipment = maintenanceTickets.Where(t => IsEquipmentActiveOnDate(t, day.Date)).OrderBy(t => t.CreatedLocal).ToList();

                var badges = new List<CalendarTicketBadgeViewModel>();
                if (maintenanceView != MaintenanceViewEquipment)
                {
                    badges.AddRange(dayAppointments.Select(CreateAppointmentBadge));
                }
                if (maintenanceView != MaintenanceViewAppointments)
                {
                    badges.AddRange(dayEquipment.Select(CreateEquipmentBadge));
                }

                model.Days.Add(new CalendarDayViewModel
                {
                    Date = day,
                    IsCurrentMonth = day.Month == selectedMonth,
                    IsToday = day.Date == nowLocal.Date,
                    IsSelected = day.Date == selectedDateLocal.Date,
                    AppointmentsCount = dayAppointments.Count,
                    EquipmentCount = dayEquipment.Count,
                    Tickets = badges
                        .OrderBy(x => x.ItemTypeLabel)
                        .ThenBy(x => x.TicketNumber)
                        .ToList()
                });
            }

            model.SelectedDateAppointments = selectedDateAppointments
                .Select(a => new CalendarMaintenanceDateItemViewModel
                {
                    Id = a.Id,
                    Number = a.AppointmentNumber,
                    EquipmentOrArea = a.AssetOrArea,
                    Site = a.Site,
                    Technician = string.IsNullOrWhiteSpace(a.AssignedTechnician) ? "Sin asignar" : a.AssignedTechnician,
                    Status = a.Status,
                    StartDate = a.ScheduledFor,
                    DetailUrl = $"/MaintenanceAppointments/Details/{a.Id}",
                    SourceType = "Cita"
                })
                .ToList();

            model.SelectedDateEquipment = equipmentFromSelectedDate
                .Select(t => new CalendarMaintenanceDateItemViewModel
                {
                    Id = t.Id,
                    Number = string.IsNullOrWhiteSpace(t.TicketNumber) ? $"TK-{t.Id:D4}" : t.TicketNumber!,
                    EquipmentOrArea = BuildEquipmentLabel(t),
                    Site = t.Site,
                    Technician = string.IsNullOrWhiteSpace(t.AssignedTechnician) ? "Sin asignar" : t.AssignedTechnician,
                    Status = GetTicketStatusLabel(t.Status),
                    StartDate = t.CreatedLocal,
                    EndDate = t.ClosedLocal,
                    DaysInWorkshop = Math.Max(1, (selectedDateLocal.Date - t.CreatedLocal.Date).Days + 1),
                    DetailUrl = $"/Tickets/Details/{t.Id}",
                    SourceType = "Equipo"
                })
                .OrderBy(x => x.StartDate)
                .ToList();

            return View("Index", model);
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

        private static string NormalizeScope(string? scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return ScopeIt;
            }

            return string.Equals(scope.Trim(), ScopeMaintenance, StringComparison.OrdinalIgnoreCase)
                ? ScopeMaintenance
                : ScopeIt;
        }

        private static string NormalizeMaintenanceView(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AllValue;
            }

            var normalized = value.Trim().ToLowerInvariant();
            return normalized is AllValue or MaintenanceViewAppointments or MaintenanceViewEquipment
                ? normalized
                : AllValue;
        }

        private static DateTime? ParseSelectedDate(string? selectedDate)
        {
            if (string.IsNullOrWhiteSpace(selectedDate))
            {
                return null;
            }

            return DateTime.TryParseExact(
                selectedDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? parsed.Date
                : null;
        }
        private static string NormalizeQuick(string? quick)
        {
            if (string.IsNullOrWhiteSpace(quick))
            {
                return AllValue;
            }

            var normalized = quick.Trim().ToLowerInvariant();

            return normalized is AllValue or QuickToday or QuickOverdue or UnassignedValue or QuickCritical or QuickMyLoad
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

        private static List<CalendarTicketProjection> ApplyQuickFilter(
            List<CalendarTicketProjection> source,
            string selectedQuick,
            DateTime nowUtc,
            string? currentUser)
        {
            return selectedQuick switch
            {
                QuickToday => source.Where(t => t.SLADeadline.Date == nowUtc.Date).ToList(),
                QuickOverdue => source.Where(t => t.SLADeadline < nowUtc).ToList(),
                UnassignedValue => source.Where(t => string.IsNullOrWhiteSpace(t.AssignedTechnician)).ToList(),
                QuickCritical => source.Where(t => t.Priority == PriorityLevel.Critical).ToList(),
                QuickMyLoad => source.Where(t =>
                    !string.IsNullOrWhiteSpace(currentUser) &&
                    !string.IsNullOrWhiteSpace(t.AssignedTechnician) &&
                    string.Equals(t.AssignedTechnician.Trim(), currentUser, StringComparison.OrdinalIgnoreCase)).ToList(),
                _ => source
            };
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
                SlaStatus.NearDue => "Proximo a Vencer",
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
                IsCritical = ticket.Priority == PriorityLevel.Critical,
                ItemTypeLabel = "Ticket",
                DetailUrl = $"/Tickets/Details/{ticket.Id}"
            };
        }

        private static CalendarTicketBadgeViewModel CreateAppointmentBadge(MaintenanceAppointmentProjection appointment)
        {
            var hour = appointment.ScheduledFor.ToString("HH:mm");
            return new CalendarTicketBadgeViewModel
            {
                TicketId = appointment.Id,
                TicketNumber = appointment.AppointmentNumber,
                SlaLabel = $"Cita {hour}",
                CssClass = "ticket-chip ticket-chip--appointment",
                IsCritical = false,
                ItemTypeLabel = "Cita",
                DetailUrl = $"/MaintenanceAppointments/Details/{appointment.Id}"
            };
        }

        private static CalendarTicketBadgeViewModel CreateEquipmentBadge(MaintenanceEquipmentProjection equipment)
        {
            var cssClass = equipment.Status switch
            {
                TicketStatus.Closed => "ticket-chip ticket-chip--maintenance-closed",
                TicketStatus.Resolved => "ticket-chip ticket-chip--maintenance-review",
                _ => "ticket-chip ticket-chip--maintenance-active"
            };

            var code = string.IsNullOrWhiteSpace(equipment.TicketNumber)
                ? $"TK-{equipment.Id:D4}"
                : equipment.TicketNumber!;

            return new CalendarTicketBadgeViewModel
            {
                TicketId = equipment.Id,
                TicketNumber = code,
                SlaLabel = BuildEquipmentLabel(equipment),
                CssClass = cssClass,
                IsCritical = equipment.Priority == PriorityLevel.Critical,
                ItemTypeLabel = "Equipo",
                DetailUrl = $"/Tickets/Details/{equipment.Id}"
            };
        }

        private static bool IsEquipmentActiveOnDate(MaintenanceEquipmentProjection equipment, DateTime date)
        {
            // "Open" means the order is created but not yet admitted into workshop flow.
            if (equipment.Status == TicketStatus.Open)
            {
                return false;
            }

            var started = equipment.CreatedLocal.Date <= date.Date;
            var notExited = !equipment.ClosedLocal.HasValue || equipment.ClosedLocal.Value.Date >= date.Date;
            return started && notExited;
        }

        private static string BuildEquipmentLabel(MaintenanceEquipmentProjection equipment)
        {
            if (!string.IsNullOrWhiteSpace(equipment.UnitCode))
            {
                return equipment.UnitCode!;
            }

            if (!string.IsNullOrWhiteSpace(equipment.DamagedElement))
            {
                return equipment.DamagedElement!;
            }

            if (!string.IsNullOrWhiteSpace(equipment.IncidentType))
            {
                return equipment.IncidentType!;
            }

            return "Equipo en mantenimiento";
        }

        private static string GetTicketStatusLabel(TicketStatus status)
        {
            return status switch
            {
                TicketStatus.Open => "Abierto",
                TicketStatus.InProgress => "En progreso",
                TicketStatus.Resolved => "Resuelto",
                TicketStatus.Closed => "Cerrado",
                _ => status.ToString()
            };
        }

        private static TimeZoneInfo ResolveBusinessTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }

        private static DateTime ToBusinessLocalTime(DateTime utcDateTime)
        {
            var utc = utcDateTime.Kind == DateTimeKind.Utc
                ? utcDateTime
                : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utc, BusinessTimeZone);
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

        private sealed class MaintenanceAppointmentProjection
        {
            public int Id { get; set; }
            public string AppointmentNumber { get; set; } = string.Empty;
            public DateTime ScheduledFor { get; set; }
            public string Site { get; set; } = string.Empty;
            public string AssetOrArea { get; set; } = string.Empty;
            public string? AssignedTechnician { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        private sealed class MaintenanceEquipmentProjection
        {
            public int Id { get; set; }
            public string? TicketNumber { get; set; }
            public string? UnitCode { get; set; }
            public string Site { get; set; } = string.Empty;
            public string? AssignedTechnician { get; set; }
            public TicketStatus Status { get; set; }
            public DateTime CreatedDateUtc { get; set; }
            public DateTime? ClosedDateUtc { get; set; }
            public string? IncidentType { get; set; }
            public string? DamagedElement { get; set; }
            public PriorityLevel Priority { get; set; }
            public DateTime SlaDeadline { get; set; }
            public DateTime CreatedLocal { get; set; }
            public DateTime? ClosedLocal { get; set; }
        }
    }
}
