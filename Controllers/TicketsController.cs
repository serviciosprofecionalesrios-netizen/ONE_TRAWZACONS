using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize]
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TicketsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, TicketStatus? status, PriorityLevel? priority)
        {
            IQueryable<Ticket> query = _context.Tickets.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(t =>
                    (t.TicketNumber != null && t.TicketNumber.Contains(search)) ||
                    t.RequestingUser.Contains(search) ||
                    t.Description.Contains(search));
            }

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (priority.HasValue)
            {
                query = query.Where(t => t.Priority == priority.Value);
            }

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentPriority = priority;

            ViewBag.OverdueCount = await _context.Tickets
                .Where(t => t.Status != TicketStatus.Closed && t.SLADeadline < DateTime.UtcNow)
                .CountAsync();

            var tickets = await query
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();

            return View(tickets);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets
                .AsNoTracking()
                .Include(t => t.HistoryEntries)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Ticket ticket)
        {
            if (!ModelState.IsValid)
            {
                return View(ticket);
            }

            ticket.CreatedDate = DateTime.UtcNow;
            ticket.SLADeadline = CalculateSLA(ticket.Priority);
            ticket.ClosedDate = ticket.Status == TicketStatus.Closed ? DateTime.UtcNow : null;

            const int maxAttempts = 3;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ticket.TicketNumber = await GenerateTicketNumberAsync();
                _context.Tickets.Add(ticket);

                try
                {
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException) when (attempt < maxAttempts)
                {
                    _context.Entry(ticket).State = EntityState.Detached;
                }
            }

            ModelState.AddModelError(string.Empty, "No fue posible crear el ticket. Intente nuevamente.");
            return View(ticket);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Ticket ticket)
        {
            if (id != ticket.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(ticket);
            }

            if (ticket.RowVersion == null)
            {
                ModelState.AddModelError(string.Empty, "No se pudo validar concurrencia del ticket.");
                return View(ticket);
            }

            var dbTicket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (dbTicket == null)
            {
                return NotFound();
            }

            var originalTicket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (originalTicket == null)
            {
                return NotFound();
            }

            _context.Entry(dbTicket).Property(t => t.RowVersion).OriginalValue = ticket.RowVersion;

            dbTicket.TicketNumber = ticket.TicketNumber;
            dbTicket.RequestingUser = ticket.RequestingUser;
            dbTicket.Site = ticket.Site;
            dbTicket.Department = ticket.Department;
            dbTicket.IncidentType = ticket.IncidentType;
            dbTicket.Priority = ticket.Priority;
            dbTicket.Status = ticket.Status;
            dbTicket.AssignedTechnician = ticket.AssignedTechnician;
            dbTicket.SLADeadline = ticket.SLADeadline;
            dbTicket.Description = ticket.Description;
            dbTicket.AttachmentPath = ticket.AttachmentPath;

            if (originalTicket.Priority != dbTicket.Priority)
            {
                dbTicket.SLADeadline = CalculateSLA(dbTicket.Priority);
            }

            if (dbTicket.Status == TicketStatus.Closed && originalTicket.Status != TicketStatus.Closed)
            {
                dbTicket.ClosedDate = DateTime.UtcNow;
            }

            if (dbTicket.Status != TicketStatus.Closed && originalTicket.Status == TicketStatus.Closed)
            {
                dbTicket.ClosedDate = null;
            }

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, dbTicket, GetChangedBy());
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "El registro fue modificado por otro usuario.");
                ticket.RowVersion = dbTicket.RowVersion;
                return View(ticket);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No fue posible guardar los cambios del ticket.");
                return View(ticket);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket != null)
            {
                _context.Tickets.Remove(ticket);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task RegisterChangesAsync(Ticket original, Ticket updated, string changedBy)
        {
            var changes = new List<TicketHistory>();

            if (original.Status != updated.Status)
            {
                changes.Add(CreateHistory(
                    updated.Id,
                    "Status",
                    original.Status.ToString(),
                    updated.Status.ToString(),
                    changedBy));
            }

            if (original.Priority != updated.Priority)
            {
                changes.Add(CreateHistory(
                    updated.Id,
                    "Priority",
                    original.Priority.ToString(),
                    updated.Priority.ToString(),
                    changedBy));
            }

            if (original.AssignedTechnician != updated.AssignedTechnician)
            {
                changes.Add(CreateHistory(
                    updated.Id,
                    "AssignedTechnician",
                    original.AssignedTechnician,
                    updated.AssignedTechnician,
                    changedBy));
            }

            if (changes.Any())
            {
                _context.TicketHistories.AddRange(changes);
                await _context.SaveChangesAsync();
            }
        }

        private static TicketHistory CreateHistory(
            int ticketId,
            string field,
            string? oldValue,
            string? newValue,
            string changedBy)
        {
            return new TicketHistory
            {
                TicketId = ticketId,
                FieldChanged = field,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedBy = changedBy,
                ChangeDate = DateTime.UtcNow
            };
        }

        private async Task<string> GenerateTicketNumberAsync()
        {
            var year = DateTime.UtcNow.Year;

            var lastTicket = await _context.Tickets
                .Where(t => t.CreatedDate.Year == year)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync();

            var nextNumber = 1;

            if (lastTicket?.TicketNumber != null)
            {
                var parts = lastTicket.TicketNumber.Split('-');

                if (parts.Length == 3 && int.TryParse(parts[2], out var lastSequence))
                {
                    nextNumber = lastSequence + 1;
                }
            }

            return $"TK-{year}-{nextNumber:D3}";
        }

        private static DateTime CalculateSLA(PriorityLevel priority)
        {
            return priority switch
            {
                PriorityLevel.Low => DateTime.UtcNow.AddHours(48),
                PriorityLevel.Medium => DateTime.UtcNow.AddHours(24),
                PriorityLevel.High => DateTime.UtcNow.AddHours(8),
                PriorityLevel.Critical => DateTime.UtcNow.AddHours(4),
                _ => DateTime.UtcNow.AddHours(24)
            };
        }

        private string GetChangedBy()
        {
            return User.Identity?.Name ?? User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value ?? "Sistema";
        }
    }
}

