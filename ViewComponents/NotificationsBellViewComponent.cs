using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.ViewComponents
{
    public class NotificationsBellViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public NotificationsBellViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var now = DateTime.UtcNow;
            var nearLimit = now.AddHours(4);

            var activeTickets = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.Status != TicketStatus.Closed)
                .Select(t => new
                {
                    t.Id,
                    t.TicketNumber,
                    t.IncidentType,
                    t.Description,
                    t.Priority,
                    t.SLADeadline
                })
                .ToListAsync();

            var notifications = new List<NotificationItemViewModel>();

            var breached = activeTickets
                .Where(t => t.SLADeadline < now)
                .OrderBy(t => t.SLADeadline)
                .Take(12)
                .Select(t => CreateBreachedNotification(t.Id, t.TicketNumber, t.IncidentType, t.Description, t.Priority, t.SLADeadline, now));

            var nearDue = activeTickets
                .Where(t => t.SLADeadline >= now && t.SLADeadline <= nearLimit)
                .OrderBy(t => t.SLADeadline)
                .Take(12)
                .Select(t => CreateNearDueNotification(t.Id, t.TicketNumber, t.IncidentType, t.Description, t.Priority, t.SLADeadline, now));

            var critical = activeTickets
                .Where(t => t.Priority == PriorityLevel.Critical && t.SLADeadline > nearLimit)
                .OrderBy(t => t.SLADeadline)
                .Take(8)
                .Select(t => CreateCriticalNotification(t.Id, t.TicketNumber, t.IncidentType, t.Description, t.Priority, t.SLADeadline, now));

            notifications.AddRange(breached);
            notifications.AddRange(nearDue);
            notifications.AddRange(critical);

            notifications = notifications
                .OrderByDescending(n => n.ReferenceDateUtc)
                .Take(20)
                .ToList();

            var total =
                activeTickets.Count(t => t.SLADeadline < now) +
                activeTickets.Count(t => t.SLADeadline >= now && t.SLADeadline <= nearLimit) +
                activeTickets.Count(t => t.Priority == PriorityLevel.Critical && t.SLADeadline > nearLimit);

            var model = new NotificationBellViewModel
            {
                TotalCount = total,
                Items = notifications
            };

            return View(model);
        }

        private static NotificationItemViewModel CreateBreachedNotification(
            int id,
            string? ticketNumber,
            string? incidentType,
            string? description,
            PriorityLevel priority,
            DateTime slaDeadline,
            DateTime now)
        {
            return new NotificationItemViewModel
            {
                TicketId = id,
                TicketNumber = SafeTicket(ticketNumber, id),
                Title = $"SLA Vencido - {SafeText(incidentType, "Incidencia")}",
                Summary = BuildSummary(description, "Ticket vencido. Requiere atencion inmediata."),
                PriorityLabel = MapPriority(priority),
                PriorityCssClass = MapPriorityCss(priority),
                CardCssClass = "notif-item notif-item--breached",
                IconCssClass = "notif-icon notif-icon--breached",
                IconSymbolCssClass = "bi bi-alarm",
                TimeLabel = BuildElapsedLabel(now - slaDeadline),
                ReferenceDateUtc = slaDeadline
            };
        }

        private static NotificationItemViewModel CreateNearDueNotification(
            int id,
            string? ticketNumber,
            string? incidentType,
            string? description,
            PriorityLevel priority,
            DateTime slaDeadline,
            DateTime now)
        {
            return new NotificationItemViewModel
            {
                TicketId = id,
                TicketNumber = SafeTicket(ticketNumber, id),
                Title = $"SLA Proximo a Vencer - {SafeText(incidentType, "Incidencia")}",
                Summary = BuildSummary(description, BuildRemainingLabel(slaDeadline - now)),
                PriorityLabel = MapPriority(priority),
                PriorityCssClass = MapPriorityCss(priority),
                CardCssClass = "notif-item notif-item--near",
                IconCssClass = "notif-icon notif-icon--near",
                IconSymbolCssClass = "bi bi-clock-history",
                TimeLabel = BuildRemainingLabel(slaDeadline - now),
                ReferenceDateUtc = slaDeadline
            };
        }

        private static NotificationItemViewModel CreateCriticalNotification(
            int id,
            string? ticketNumber,
            string? incidentType,
            string? description,
            PriorityLevel priority,
            DateTime slaDeadline,
            DateTime now)
        {
            return new NotificationItemViewModel
            {
                TicketId = id,
                TicketNumber = SafeTicket(ticketNumber, id),
                Title = $"Prioridad Critica - {SafeText(incidentType, "Incidencia")}",
                Summary = BuildSummary(description, "Ticket critico en seguimiento."),
                PriorityLabel = MapPriority(priority),
                PriorityCssClass = MapPriorityCss(priority),
                CardCssClass = "notif-item notif-item--critical",
                IconCssClass = "notif-icon notif-icon--critical",
                IconSymbolCssClass = "bi bi-exclamation-octagon",
                TimeLabel = BuildRemainingLabel(slaDeadline - now),
                ReferenceDateUtc = slaDeadline
            };
        }

        private static string SafeTicket(string? value, int id)
        {
            return string.IsNullOrWhiteSpace(value) ? $"TK-{id:D4}" : value.Trim();
        }

        private static string SafeText(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string BuildSummary(string? description, string fallback)
        {
            var text = string.IsNullOrWhiteSpace(description) ? fallback : description.Trim();
            if (text.Length <= 95)
            {
                return text;
            }

            return text[..95] + "...";
        }

        private static string BuildElapsedLabel(TimeSpan elapsed)
        {
            if (elapsed.TotalMinutes < 60)
            {
                return $"Hace {Math.Max(1, (int)elapsed.TotalMinutes)} min";
            }

            if (elapsed.TotalHours < 24)
            {
                return $"Hace {(int)elapsed.TotalHours} h";
            }

            return $"Hace {(int)elapsed.TotalDays} d";
        }

        private static string BuildRemainingLabel(TimeSpan remaining)
        {
            if (remaining.TotalMinutes <= 0)
            {
                return "Ahora";
            }

            if (remaining.TotalMinutes < 60)
            {
                return $"Vence en {Math.Max(1, (int)remaining.TotalMinutes)} min";
            }

            if (remaining.TotalHours < 24)
            {
                return $"Vence en {(int)remaining.TotalHours} h";
            }

            return $"Vence en {(int)remaining.TotalDays} d";
        }

        private static string MapPriority(PriorityLevel priority)
        {
            return priority switch
            {
                PriorityLevel.Critical => "Critica",
                PriorityLevel.High => "Alta",
                PriorityLevel.Medium => "Media",
                _ => "Baja"
            };
        }

        private static string MapPriorityCss(PriorityLevel priority)
        {
            return priority switch
            {
                PriorityLevel.Critical => "notif-priority notif-priority--critical",
                PriorityLevel.High => "notif-priority notif-priority--high",
                PriorityLevel.Medium => "notif-priority notif-priority--medium",
                _ => "notif-priority notif-priority--low"
            };
        }
    }
}
