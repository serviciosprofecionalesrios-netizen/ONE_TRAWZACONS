using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly IWhatsAppNotificationService _whatsAppNotificationService;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IEmailNotificationService emailNotificationService,
            IWhatsAppNotificationService whatsAppNotificationService,
            ILogger<TicketsController> logger)
        {
            _context = context;
            _environment = environment;
            _emailNotificationService = emailNotificationService;
            _whatsAppNotificationService = whatsAppNotificationService;
            _logger = logger;
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

        public async Task<IActionResult> DownloadPdf(int id)
        {
            var ticket = await _context.Tickets
                .AsNoTracking()
                .Include(t => t.HistoryEntries)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            var pdfBytes = TicketPdfReportService.GenerateTicketReportPdf(ticket, _environment.WebRootPath);
            var safeTicket = string.IsNullOrWhiteSpace(ticket.TicketNumber)
                ? $"Ticket_{ticket.Id}"
                : ticket.TicketNumber;

            return File(pdfBytes, "application/pdf", $"Reporte_{safeTicket}.pdf");
        }

        public async Task<IActionResult> Create()
        {
            await PopulateCreateFormDataAsync();
            return View(new Ticket { Status = TicketStatus.Open });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Ticket ticket,
            IFormFile? reportEvidenceFile,
            IFormFile? beforeEvidenceFile,
            IFormFile? afterEvidenceFile,
            IFormFile? technicalSheetFile,
            IFormFile? exitOrderFile)
        {
            var canEditSolutionForm = CanEditSolutionForm();
            var canSetPriority = CanSetPriority();

            ticket.Status = TicketStatus.Open;
            if (!canSetPriority)
            {
                ticket.Priority = PriorityLevel.Medium;
            }

            NormalizeTechnicalFieldsForRole(ticket, canEditSolutionForm);
            NormalizeSparePartFields(ticket);
            NormalizeComponentChangeFields(ticket);
            ValidateSolutionBusinessRules(ticket, null, afterEvidenceFile);

            if (!ModelState.IsValid)
            {
                await PopulateCreateFormDataAsync(ticket);
                return View(ticket);
            }

            ticket.AttachmentPath = await SaveUploadedFileAsync(reportEvidenceFile, ticket.AttachmentPath, new[] { ".jpg", ".jpeg", ".png", ".webp" });

            if (canEditSolutionForm)
            {
                ticket.BeforeEvidencePath = await SaveUploadedFileAsync(beforeEvidenceFile, ticket.BeforeEvidencePath);
                ticket.AfterEvidencePath = await SaveUploadedFileAsync(afterEvidenceFile, ticket.AfterEvidencePath);
                ticket.TechnicalSheetPath = await SaveUploadedFileAsync(technicalSheetFile, ticket.TechnicalSheetPath, new[] { ".jpg", ".jpeg", ".png", ".webp" });
                ticket.ExitOrderPath = await SaveUploadedFileAsync(exitOrderFile, ticket.ExitOrderPath, new[] { ".jpg", ".jpeg", ".png", ".webp" });
            }
            else
            {
                ticket.BeforeEvidencePath = null;
                ticket.AfterEvidencePath = null;
                ticket.TechnicalSheetPath = null;
                ticket.ExitOrderPath = null;
                ticket.Observations = null;
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
                    await NotifyNewTicketAsync(ticket);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException) when (attempt < maxAttempts)
                {
                    _context.Entry(ticket).State = EntityState.Detached;
                }
            }

            await PopulateCreateFormDataAsync(ticket);
            ModelState.AddModelError(string.Empty, "No fue posible crear el ticket. Intente nuevamente.");
            return View(ticket);
        }

        [Authorize(Roles = "Administrator,Technician,CoordinadorIT")]
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

            await PopulateCreateFormDataAsync(ticket);
            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Technician,CoordinadorIT")]
        public async Task<IActionResult> Edit(
            int id,
            Ticket ticket,
            IFormFile? attachmentFile,
            IFormFile? beforeEvidenceFile,
            IFormFile? afterEvidenceFile,
            IFormFile? technicalSheetFile,
            IFormFile? exitOrderFile)
        {
            if (id != ticket.Id)
            {
                return NotFound();
            }

            if (ticket.RowVersion == null)
            {
                ModelState.AddModelError(string.Empty, "No se pudo validar concurrencia del ticket.");
                await PopulateCreateFormDataAsync(ticket);
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

            var canEditSolutionForm = CanEditSolutionForm();
            var canSetPriority = CanSetPriority();

            if (!canSetPriority)
            {
                ticket.Priority = originalTicket.Priority;
            }

            NormalizeTechnicalFieldsForRole(ticket, canEditSolutionForm, originalTicket);
            NormalizeSparePartFields(ticket);
            NormalizeComponentChangeFields(ticket);
            ValidateSolutionBusinessRules(ticket, originalTicket.AfterEvidencePath, afterEvidenceFile);

            if (!ModelState.IsValid)
            {
                await PopulateCreateFormDataAsync(ticket);
                return View(ticket);
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
            dbTicket.SLADeadline = originalTicket.SLADeadline;
            dbTicket.Description = ticket.Description;

            dbTicket.AttachmentPath = await SaveUploadedFileAsync(
                attachmentFile,
                string.IsNullOrWhiteSpace(ticket.AttachmentPath)
                    ? originalTicket.AttachmentPath
                    : ticket.AttachmentPath);

            if (canEditSolutionForm)
            {
                dbTicket.InitialConditionNotes = ticket.InitialConditionNotes;
                dbTicket.RepairActionsPerformed = ticket.RepairActionsPerformed;
                dbTicket.RootCause = ticket.RootCause;
                dbTicket.SparePartRequired = ticket.SparePartRequired;
                dbTicket.SparePartPurchased = ticket.SparePartPurchased;
                dbTicket.SparePartDetails = ticket.SparePartDetails;
                dbTicket.ComponentChanged = ticket.ComponentChanged;
                dbTicket.ChangedComponentName = ticket.ChangedComponentName;
                dbTicket.ChangedComponentCost = ticket.ChangedComponentCost;
                dbTicket.ChangedComponentCostCordoba = ticket.ChangedComponentCostCordoba;
                dbTicket.ChangedComponentCostUsd = ticket.ChangedComponentCostUsd;
                dbTicket.TechnicalTestsPerformed = ticket.TechnicalTestsPerformed;
                dbTicket.UserConformityConfirmed = ticket.UserConformityConfirmed;
                dbTicket.PreventiveRecommendations = ticket.PreventiveRecommendations;
                dbTicket.Observations = ticket.Observations;

                dbTicket.BeforeEvidencePath = await SaveUploadedFileAsync(
                    beforeEvidenceFile,
                    string.IsNullOrWhiteSpace(ticket.BeforeEvidencePath)
                        ? originalTicket.BeforeEvidencePath
                        : ticket.BeforeEvidencePath);

                dbTicket.AfterEvidencePath = await SaveUploadedFileAsync(
                    afterEvidenceFile,
                    string.IsNullOrWhiteSpace(ticket.AfterEvidencePath)
                        ? originalTicket.AfterEvidencePath
                        : ticket.AfterEvidencePath);

                dbTicket.TechnicalSheetPath = await SaveUploadedFileAsync(
                    technicalSheetFile,
                    string.IsNullOrWhiteSpace(ticket.TechnicalSheetPath)
                        ? originalTicket.TechnicalSheetPath
                        : ticket.TechnicalSheetPath,
                    new[] { ".jpg", ".jpeg", ".png", ".webp" });

                dbTicket.ExitOrderPath = await SaveUploadedFileAsync(
                    exitOrderFile,
                    string.IsNullOrWhiteSpace(ticket.ExitOrderPath)
                        ? originalTicket.ExitOrderPath
                        : ticket.ExitOrderPath,
                    new[] { ".jpg", ".jpeg", ".png", ".webp" });
            }
            else
            {
                dbTicket.InitialConditionNotes = originalTicket.InitialConditionNotes;
                dbTicket.RepairActionsPerformed = originalTicket.RepairActionsPerformed;
                dbTicket.RootCause = originalTicket.RootCause;
                dbTicket.SparePartRequired = originalTicket.SparePartRequired;
                dbTicket.SparePartPurchased = originalTicket.SparePartPurchased;
                dbTicket.SparePartDetails = originalTicket.SparePartDetails;
                dbTicket.ComponentChanged = originalTicket.ComponentChanged;
                dbTicket.ChangedComponentName = originalTicket.ChangedComponentName;
                dbTicket.ChangedComponentCost = originalTicket.ChangedComponentCost;
                dbTicket.ChangedComponentCostCordoba = originalTicket.ChangedComponentCostCordoba;
                dbTicket.ChangedComponentCostUsd = originalTicket.ChangedComponentCostUsd;
                dbTicket.TechnicalTestsPerformed = originalTicket.TechnicalTestsPerformed;
                dbTicket.UserConformityConfirmed = originalTicket.UserConformityConfirmed;
                dbTicket.PreventiveRecommendations = originalTicket.PreventiveRecommendations;
                dbTicket.Observations = originalTicket.Observations;
                dbTicket.BeforeEvidencePath = originalTicket.BeforeEvidencePath;
                dbTicket.AfterEvidencePath = originalTicket.AfterEvidencePath;
                dbTicket.TechnicalSheetPath = originalTicket.TechnicalSheetPath;
                dbTicket.ExitOrderPath = originalTicket.ExitOrderPath;
            }

            if (canEditSolutionForm &&
                dbTicket.Status == TicketStatus.Resolved &&
                IsTechnicalFormCompleted(dbTicket))
            {
                dbTicket.Status = TicketStatus.Closed;
            }

            if (CanModifyAttentionDate() && originalTicket.Priority != dbTicket.Priority)
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
                await PopulateCreateFormDataAsync(ticket);
                return View(ticket);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No fue posible guardar los cambios del ticket.");
                await PopulateCreateFormDataAsync(ticket);
                return View(ticket);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrator,CoordinadorIT")]
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
        [Authorize(Roles = "Administrator,CoordinadorIT")]
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

        private async Task PopulateCreateFormDataAsync(Ticket? ticket = null)
        {
            ViewBag.CanEditSolutionForm = CanEditSolutionForm();
            ViewBag.CanSetPriority = CanSetPriority();
            ViewBag.PreviewCreatedDate = DateTime.Now;
            ViewBag.PreviewTicketNumber = await GenerateTicketNumberAsync();
            ViewBag.SlaPreview = ToBusinessLocalTime(CalculateSLA(PriorityLevel.Medium)).ToString("dd/MM/yyyy HH:mm");

            var requestingUsers = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new { u.FullName, u.Email })
                .ToListAsync();

            var requestingUserOptions = requestingUsers
                .Select(u => new SelectListItem($"{u.FullName} ({u.Email})", u.FullName))
                .ToList();

            if (!string.IsNullOrWhiteSpace(ticket?.RequestingUser) &&
                requestingUserOptions.All(o => o.Value != ticket.RequestingUser))
            {
                requestingUserOptions.Insert(
                    0,
                    new SelectListItem($"{ticket.RequestingUser} (No activo)", ticket.RequestingUser));
            }

            var technicians = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && (
                    u.Role == UserRole.Technician ||
                    u.Role == UserRole.CoordinadorIT ||
                    u.Role == UserRole.Administrator))
                .OrderBy(u => u.FullName)
                .Select(u => u.FullName)
                .Distinct()
                .ToListAsync();

            var sites = new List<string>
            {
                "Oficina ASOMA",
                "Plantel Nagarote",
                "Plantel Las Lajitas",
                "Granada",
                "Jinotepe",
                "Casa Miramar",
                "Casa de Alto Nagarote"
            };

            if (!string.IsNullOrWhiteSpace(ticket?.Site) &&
                !sites.Contains(ticket.Site, StringComparer.OrdinalIgnoreCase))
            {
                sites.Insert(0, ticket.Site);
            }

            var baseDepartments = new List<string>
            {
                "IT",
                "Operaciones",
                "Administracion",
                "Compras",
                "Contabilidad"
            };

            var dbDepartments = await _context.Tickets
                .AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.Department))
                .Select(t => t.Department!.Trim())
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var departments = baseDepartments
                .Concat(dbDepartments.Where(d => !baseDepartments.Contains(d, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var baseIncidentTypes = new List<string>
            {
                "Hardware",
                "Software",
                "Red",
                "Servidores",
                "Otros"
            };

            var dbIncidentTypes = await _context.Tickets
                .AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.IncidentType))
                .Select(t => t.IncidentType!.Trim())
                .Distinct()
                .OrderBy(i => i)
                .ToListAsync();

            var incidentTypes = baseIncidentTypes
                .Concat(dbIncidentTypes.Where(i => !baseIncidentTypes.Contains(i, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(ticket?.Department) &&
                departments.All(d => !d.Equals(ticket.Department, StringComparison.OrdinalIgnoreCase)))
            {
                departments.Insert(0, ticket.Department);
            }

            if (!string.IsNullOrWhiteSpace(ticket?.IncidentType) &&
                incidentTypes.All(i => !i.Equals(ticket.IncidentType, StringComparison.OrdinalIgnoreCase)))
            {
                incidentTypes.Insert(0, ticket.IncidentType);
            }

            ViewBag.RequestingUserOptions = requestingUserOptions;

            ViewBag.SiteOptions = sites
                .Select(s => new SelectListItem(s, s))
                .ToList();

            ViewBag.DepartmentOptions = departments
                .Select(d => new SelectListItem(d, d))
                .ToList();

            ViewBag.IncidentTypeOptions = incidentTypes
                .Select(i => new SelectListItem(i, i))
                .ToList();

            ViewBag.TechnicianOptions = technicians
                .Select(t => new SelectListItem(t, t))
                .ToList();
        }

        private async Task<string?> SaveUploadedFileAsync(IFormFile? file, string? currentPath, string[]? allowedExtensions = null)
        {
            if (file == null || file.Length == 0)
            {
                return currentPath;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            allowedExtensions ??= new[] { ".jpg", ".jpeg", ".png", ".pdf" };

            if (!allowedExtensions.Contains(extension))
            {
                return currentPath;
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                return currentPath;
            }

            var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "evidence");
            Directory.CreateDirectory(uploadsRoot);

            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadsRoot, uniqueFileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/evidence/{uniqueFileName}";
        }

        private static void NormalizeSparePartFields(Ticket ticket)
        {
            if (!ticket.SparePartRequired)
            {
                ticket.SparePartPurchased = false;
                ticket.SparePartDetails = null;
            }
        }

        private static void NormalizeComponentChangeFields(Ticket ticket)
        {
            if (!ticket.ComponentChanged)
            {
                ticket.ChangedComponentName = null;
                ticket.ChangedComponentCost = null;
                ticket.ChangedComponentCostCordoba = null;
                ticket.ChangedComponentCostUsd = null;
                return;
            }

            if (!ticket.ChangedComponentCostUsd.HasValue && ticket.ChangedComponentCost.HasValue)
            {
                ticket.ChangedComponentCostUsd = ticket.ChangedComponentCost;
            }

            ticket.ChangedComponentCost = ticket.ChangedComponentCostUsd;
        }

        private static void NormalizeTechnicalFieldsForRole(
            Ticket ticket,
            bool canEditSolutionForm,
            Ticket? originalTicket = null)
        {
            if (canEditSolutionForm)
            {
                return;
            }

            if (originalTicket == null)
            {
                ticket.InitialConditionNotes = null;
                ticket.RepairActionsPerformed = null;
                ticket.RootCause = null;
                ticket.SparePartRequired = false;
                ticket.SparePartPurchased = false;
                ticket.SparePartDetails = null;
                ticket.ComponentChanged = false;
                ticket.ChangedComponentName = null;
                ticket.ChangedComponentCost = null;
                ticket.ChangedComponentCostCordoba = null;
                ticket.ChangedComponentCostUsd = null;
                ticket.TechnicalTestsPerformed = null;
                ticket.UserConformityConfirmed = false;
                ticket.PreventiveRecommendations = null;
                ticket.Observations = null;
                ticket.BeforeEvidencePath = null;
                ticket.AfterEvidencePath = null;
                ticket.TechnicalSheetPath = null;
                ticket.ExitOrderPath = null;
                return;
            }

            ticket.InitialConditionNotes = originalTicket.InitialConditionNotes;
            ticket.RepairActionsPerformed = originalTicket.RepairActionsPerformed;
            ticket.RootCause = originalTicket.RootCause;
            ticket.SparePartRequired = originalTicket.SparePartRequired;
            ticket.SparePartPurchased = originalTicket.SparePartPurchased;
            ticket.SparePartDetails = originalTicket.SparePartDetails;
            ticket.ComponentChanged = originalTicket.ComponentChanged;
            ticket.ChangedComponentName = originalTicket.ChangedComponentName;
            ticket.ChangedComponentCost = originalTicket.ChangedComponentCost;
            ticket.ChangedComponentCostCordoba = originalTicket.ChangedComponentCostCordoba;
            ticket.ChangedComponentCostUsd = originalTicket.ChangedComponentCostUsd;
            ticket.TechnicalTestsPerformed = originalTicket.TechnicalTestsPerformed;
            ticket.UserConformityConfirmed = originalTicket.UserConformityConfirmed;
            ticket.PreventiveRecommendations = originalTicket.PreventiveRecommendations;
            ticket.Observations = originalTicket.Observations;
            ticket.BeforeEvidencePath = originalTicket.BeforeEvidencePath;
            ticket.AfterEvidencePath = originalTicket.AfterEvidencePath;
            ticket.TechnicalSheetPath = originalTicket.TechnicalSheetPath;
            ticket.ExitOrderPath = originalTicket.ExitOrderPath;
        }

        private void ValidateSolutionBusinessRules(
            Ticket ticket,
            string? currentAfterEvidencePath,
            IFormFile? afterEvidenceFile)
        {
            var requiresFinalEvidence =
                ticket.Status == TicketStatus.Resolved ||
                ticket.Status == TicketStatus.Closed;

            var hasFinalEvidence =
                !string.IsNullOrWhiteSpace(ticket.AfterEvidencePath) ||
                !string.IsNullOrWhiteSpace(currentAfterEvidencePath) ||
                (afterEvidenceFile != null && afterEvidenceFile.Length > 0);

            if (requiresFinalEvidence && !hasFinalEvidence)
            {
                ModelState.AddModelError(
                    nameof(Ticket.AfterEvidencePath),
                    "Debe adjuntar evidencia final cuando la incidencia est? Resuelta o Cerrada.");
            }

            if (ticket.SparePartRequired && string.IsNullOrWhiteSpace(ticket.SparePartDetails))
            {
                ModelState.AddModelError(
                    nameof(Ticket.SparePartDetails),
                    "Debe especificar el detalle del repuesto cuando marca que fue requerido.");
            }

            if (ticket.ComponentChanged && string.IsNullOrWhiteSpace(ticket.ChangedComponentName))
            {
                ModelState.AddModelError(
                    nameof(Ticket.ChangedComponentName),
                    "Debe indicar cual componente fue cambiado.");
            }

            if (ticket.ComponentChanged && ticket.ChangedComponentCostCordoba == null)
            {
                ModelState.AddModelError(
                    nameof(Ticket.ChangedComponentCostCordoba),
                    "Debe indicar el costo del componente cambiado en C$.");
            }

            if (ticket.ComponentChanged && ticket.ChangedComponentCostUsd == null)
            {
                ModelState.AddModelError(
                    nameof(Ticket.ChangedComponentCostUsd),
                    "Debe indicar el costo del componente cambiado en $.");
            }

        }

        private static bool IsTechnicalFormCompleted(Ticket ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket.InitialConditionNotes) ||
                string.IsNullOrWhiteSpace(ticket.RepairActionsPerformed) ||
                string.IsNullOrWhiteSpace(ticket.RootCause) ||
                string.IsNullOrWhiteSpace(ticket.TechnicalTestsPerformed) ||
                !ticket.UserConformityConfirmed ||
                string.IsNullOrWhiteSpace(ticket.AfterEvidencePath))
            {
                return false;
            }

            if (ticket.SparePartRequired && string.IsNullOrWhiteSpace(ticket.SparePartDetails))
            {
                return false;
            }

            if (ticket.ComponentChanged &&
                (string.IsNullOrWhiteSpace(ticket.ChangedComponentName) ||
                 !ticket.ChangedComponentCostCordoba.HasValue ||
                 !ticket.ChangedComponentCostUsd.HasValue))
            {
                return false;
            }

            return true;
        }
        private bool CanSetPriority()
        {
            return User.IsInRole(nameof(UserRole.Technician)) ||
                   User.IsInRole(nameof(UserRole.Administrator));
        }
        private bool CanEditSolutionForm()
        {
            return User.IsInRole(nameof(UserRole.Technician)) ||
                   User.IsInRole(nameof(UserRole.Administrator));
        }

        private bool CanModifyAttentionDate()
        {
            return User.IsInRole(nameof(UserRole.Administrator)) ||
                   User.IsInRole(nameof(UserRole.CoordinadorIT));
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

        private static readonly TimeZoneInfo SlaBusinessTimeZone = ResolveSlaBusinessTimeZone();

        private static DateTime CalculateSLA(PriorityLevel priority)
        {
            var slaHours = GetSlaHours(priority);
            var nowBusiness = ToBusinessLocalTime(DateTime.UtcNow);
            var start = MoveToNextWorkingMinute(nowBusiness);
            var deadlineBusiness = AddWorkingHours(start, slaHours);

            return TimeZoneInfo.ConvertTimeToUtc(deadlineBusiness, SlaBusinessTimeZone);
        }

        private static TimeZoneInfo ResolveSlaBusinessTimeZone()
        {
            try
            {
                // Nicaragua: UTC-06:00 sin DST.
                return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }

        private static DateTime ToBusinessLocalTime(DateTime utcDateTime)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, SlaBusinessTimeZone);
        }

        private static int GetSlaHours(PriorityLevel priority)
        {
            return priority switch
            {
                PriorityLevel.Low => 48,
                PriorityLevel.Medium => 24,
                PriorityLevel.High => 8,
                PriorityLevel.Critical => 4,
                _ => 24
            };
        }

        private static DateTime MoveToNextWorkingMinute(DateTime localDateTime)
        {
            var current = localDateTime;

            while (true)
            {
                if (current.DayOfWeek == DayOfWeek.Sunday)
                {
                    current = current.Date.AddDays(1).AddHours(8);
                    continue;
                }

                var start = current.Date.AddHours(8);
                var end = current.Date.AddHours(current.DayOfWeek == DayOfWeek.Saturday ? 13 : 17);

                if (current < start)
                {
                    return start;
                }

                if (current >= end)
                {
                    current = current.Date.AddDays(1).AddHours(8);
                    continue;
                }

                return current;
            }
        }

        private static DateTime AddWorkingHours(DateTime startLocal, int hours)
        {
            var remaining = (double)hours;
            var current = MoveToNextWorkingMinute(startLocal);

            while (remaining > 0)
            {
                current = MoveToNextWorkingMinute(current);

                var shiftEnd = current.Date.AddHours(current.DayOfWeek == DayOfWeek.Saturday ? 13 : 17);
                var available = (shiftEnd - current).TotalHours;

                if (available <= 0)
                {
                    current = current.Date.AddDays(1).AddHours(8);
                    continue;
                }

                var consumed = Math.Min(remaining, available);
                current = current.AddHours(consumed);
                remaining -= consumed;

                if (remaining > 0)
                {
                    current = current.Date.AddDays(1).AddHours(8);
                }
            }

            return current;
        }

        private async Task NotifyNewTicketAsync(Ticket ticket)
        {
            try
            {
                await _emailNotificationService.NotifyNewTicketAsync(ticket, GetChangedBy());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar notificacion de correo para ticket {TicketNumber}", ticket.TicketNumber);
            }

            try
            {
                await _whatsAppNotificationService.NotifyNewTicketAsync(ticket, GetChangedBy());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar notificacion de WhatsApp para ticket {TicketNumber}", ticket.TicketNumber);
            }
        }
        private string GetChangedBy()
        {
            return User.Identity?.Name ?? User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value ?? "Sistema";
        }
    }
}






























