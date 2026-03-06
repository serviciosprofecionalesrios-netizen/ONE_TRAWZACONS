using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using System.Collections.Generic;

namespace ITServiceDeskApp.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TicketsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // INDEX
        // =====================================================

        public async Task<IActionResult> Index(string? search, TicketStatus? status, PriorityLevel? priority)
        {
            IQueryable<Ticket> query = _context.Tickets;

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(t =>
                    (t.TicketNumber != null && t.TicketNumber.Contains(search)) ||
                    t.RequestingUser.Contains(search) ||
                    t.Description.Contains(search));
            }

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            if (priority.HasValue)
                query = query.Where(t => t.Priority == priority.Value);

            ViewBag.OverdueCount = await _context.Tickets
                .Where(t => t.Status != TicketStatus.Closed &&
                            t.SLADeadline < DateTime.UtcNow)
                .CountAsync();

            var tickets = await query
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();

            return View(tickets);
        }

        // =====================================================
        // DETAILS (INCLUYE HISTORIAL CORRECTAMENTE)
        // =====================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var ticket = await _context.Tickets
                .Include(t => t.History) // ✅ CORREGIDO
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            return View(ticket);
        }

        // =====================================================
        // CREATE
        // =====================================================

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Ticket ticket)
        {
            if (!ModelState.IsValid)
                return View(ticket);

            ticket.TicketNumber = await GenerateTicketNumberAsync();
            ticket.CreatedDate = DateTime.UtcNow;
            ticket.SLADeadline = CalculateSLA(ticket.Priority);

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // EDIT
        // =====================================================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Ticket ticket)
        {
            if (id != ticket.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(ticket);

            var originalTicket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (originalTicket == null)
                return NotFound();

            try
            {
                if (originalTicket.Priority != ticket.Priority)
                {
                    ticket.SLADeadline = CalculateSLA(ticket.Priority);
                }

                if (ticket.Status == TicketStatus.Closed &&
                    originalTicket.Status != TicketStatus.Closed)
                {
                    ticket.ClosedDate = DateTime.UtcNow;
                }

                if (ticket.Status != TicketStatus.Closed &&
                    originalTicket.Status == TicketStatus.Closed)
                {
                    ticket.ClosedDate = null;
                }

                _context.Entry(ticket).Property("RowVersion").OriginalValue = ticket.RowVersion;

                _context.Update(ticket);
                await _context.SaveChangesAsync();

                await RegisterChangesAsync(originalTicket, ticket);
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("",
                    "El registro fue modificado por otro usuario.");
                return View(ticket);
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // DELETE
        // =====================================================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var ticket = await _context.Tickets
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

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

        // =====================================================
        // HISTORIAL AUTOMÁTICO
        // =====================================================

        private async Task RegisterChangesAsync(Ticket original, Ticket updated)
        {
            var changes = new List<TicketHistory>();

            if (original.Status != updated.Status)
            {
                changes.Add(CreateHistory(updated.Id, "Status",
                    original.Status.ToString(),
                    updated.Status.ToString()));
            }

            if (original.Priority != updated.Priority)
            {
                changes.Add(CreateHistory(updated.Id, "Priority",
                    original.Priority.ToString(),
                    updated.Priority.ToString()));
            }

            if (original.AssignedTechnician != updated.AssignedTechnician)
            {
                changes.Add(CreateHistory(updated.Id, "AssignedTechnician",
                    original.AssignedTechnician,
                    updated.AssignedTechnician));
            }

            if (changes.Any())
            {
                _context.TicketHistories.AddRange(changes);
                await _context.SaveChangesAsync();
            }
        }

        private TicketHistory CreateHistory(int ticketId, string field, string? oldValue, string? newValue)
        {
            return new TicketHistory
            {
                TicketId = ticketId,
                FieldChanged = field,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedBy = "Sistema",
                ChangeDate = DateTime.UtcNow
            };
        }

        // =====================================================
        // UTILIDADES
        // =====================================================

        private async Task<string> GenerateTicketNumberAsync()
        {
            var year = DateTime.UtcNow.Year;

            var lastTicket = await _context.Tickets
                .Where(t => t.CreatedDate.Year == year)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync();

            int nextNumber = 1;

            if (lastTicket?.TicketNumber != null)
            {
                var parts = lastTicket.TicketNumber.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int lastSequence))
                    nextNumber = lastSequence + 1;
            }

            return $"TK-{year}-{nextNumber:D3}";
        }

        private DateTime CalculateSLA(PriorityLevel priority)
        {
            return priority switch
            {
                PriorityLevel.Low => DateTime.UtcNow.AddHours(48),
                PriorityLevel.Medium => DateTime.UtcNow.AddHours(24),
                PriorityLevel.High => DateTime.UtcNow.AddHours(8),
                _ => DateTime.UtcNow.AddHours(24)
            };
        }
    }
}