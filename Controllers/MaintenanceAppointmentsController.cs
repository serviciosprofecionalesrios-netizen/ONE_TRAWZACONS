using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Services.Interfaces;
using ITServiceDeskApp.ViewModels.MaintenanceAppointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class MaintenanceAppointmentsController : Controller
    {
        private const string AllValue = "all";
        private const string UnassignedValue = "unassigned";

        private static readonly string[] SiteOptions =
        {
            "PCT",
            "PSB",
            "POZ"
        };

        private static readonly string[] TenenciaOptions =
        {
            "Propio",
            "Agregado"
        };

        private static readonly string[] TypeOptions =
        {
            "Preventivo",
            "Correctivo",
            "Inspeccion",
            "Emergencia",
            "Otro"
        };

        private static readonly string[] StatusOptions =
        {
            "Programada",
            "Confirmada",
            "En ejecucion",
            "Completada",
            "Reprogramada",
            "Cancelada"
        };

        private static readonly Dictionary<string, string> LegacyStatusMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ejecutada"] = "Completada",
            ["En Proceso"] = "En ejecucion",
            ["En progreso"] = "En ejecucion"
        };

        private static readonly Dictionary<string, AppointmentTemplatePreset> TemplatePresets =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["frenos"] = new("Mantenimiento de frenos", "Revision integral del sistema de frenos, ajuste, cambio de piezas y prueba de ruta.", 4m, "Correctivo"),
                ["suspension"] = new("Revision de suspension", "Inspeccion y correccion de suspension, bujes, amortiguadores y alineacion.", 5m, "Preventivo"),
                ["electrico"] = new("Diagnostico electrico", "Diagnostico electrico completo, revision de alternador, baterias y sistema de carga.", 3m, "Correctivo"),
                ["lubricacion"] = new("Lubricacion y engrase", "Lubricacion preventiva de componentes, engrase y verificacion de puntos criticos.", 2m, "Preventivo"),
                ["llantas"] = new("Llantas y rodaje", "Inspeccion de llantas, desgaste, presion y rotacion con reporte de hallazgos.", 2.5m, "Preventivo")
            };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly ILogger<MaintenanceAppointmentsController> _logger;

        private sealed record AppointmentTemplatePreset(string Label, string Description, decimal DurationHours, string MaintenanceType);
        private sealed record AppointmentAuditMutation(string Field, string? OldValue, string? NewValue, string? Reason);

        private const string AuditTableName = "dbo.MaintenanceAppointmentAudits";
        private const string ScheduleFieldKey = "Fecha y hora de cita";

        public MaintenanceAppointmentsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IEmailNotificationService emailNotificationService,
            ILogger<MaintenanceAppointmentsController> logger)
        {
            _context = context;
            _environment = environment;
            _emailNotificationService = emailNotificationService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(
            string? status = null,
            string? technician = null,
            string? site = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var nowLocal = DateTime.Now;

            var rows = await _context.MaintenanceAppointments
                .OrderBy(x => x.ScheduledFor)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();

            await EnsureAutomaticRemindersAsync(rows, nowLocal);

            var normalizedStatusFilter = string.IsNullOrWhiteSpace(status) ? null : NormalizeStatusValue(status);
            var normalizedTechnicianFilter = string.IsNullOrWhiteSpace(technician) ? AllValue : technician.Trim();
            var normalizedSiteFilter = string.IsNullOrWhiteSpace(site) ? AllValue : site.Trim();

            var normalizedRows = rows
                .Select(row =>
                {
                    row.Status = NormalizeStatusValue(row.Status);
                    var (meta, userNotes) = MaintenanceAppointmentMetadataHelper.Parse(row.Notes);
                    return BuildListItem(row, meta, userNotes, nowLocal);
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(normalizedStatusFilter))
                normalizedRows = normalizedRows.Where(x => string.Equals(x.Appointment.Status, normalizedStatusFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.Equals(normalizedTechnicianFilter, AllValue, StringComparison.OrdinalIgnoreCase))
                normalizedRows = string.Equals(normalizedTechnicianFilter, UnassignedValue, StringComparison.OrdinalIgnoreCase)
                    ? normalizedRows.Where(x => !x.HasAssignedTechnician).ToList()
                    : normalizedRows.Where(x => string.Equals((x.Appointment.AssignedTechnician ?? string.Empty).Trim(), normalizedTechnicianFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.Equals(normalizedSiteFilter, AllValue, StringComparison.OrdinalIgnoreCase))
                normalizedRows = normalizedRows.Where(x => string.Equals(x.Appointment.Site, normalizedSiteFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (dateFrom.HasValue)
                normalizedRows = normalizedRows.Where(x => x.Appointment.ScheduledFor.Date >= dateFrom.Value.Date).ToList();
            if (dateTo.HasValue)
                normalizedRows = normalizedRows.Where(x => x.Appointment.ScheduledFor.Date <= dateTo.Value.Date).ToList();

            var technicians = await _context.MaintenanceTechnicians
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => x.FullName.Trim())
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            if (technicians.Count == 0)
            {
                technicians = normalizedRows
                    .Select(x => x.Appointment.AssignedTechnician)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();
            }

            var completedRows = normalizedRows.Where(x => x.IsCompleted).ToList();
            var completedWithExit = completedRows.Where(x => x.Metadata.WorkshopExitAt.HasValue).ToList();
            var onTimeRate = completedWithExit.Count == 0
                ? 0
                : Math.Round(completedWithExit.Count(x => x.Metadata.WorkshopExitAt!.Value <= x.Appointment.ScheduledFor) * 100.0 / completedWithExit.Count, 1);

            var model = new MaintenanceAppointmentsIndexViewModel
            {
                SelectedStatus = normalizedStatusFilter,
                SelectedTechnician = normalizedTechnicianFilter,
                SelectedSite = normalizedSiteFilter,
                DateFrom = dateFrom,
                DateTo = dateTo,
                Rows = normalizedRows.OrderBy(x => x.Appointment.ScheduledFor).ThenBy(x => x.Appointment.AppointmentNumber).ToList(),
                TotalCount = normalizedRows.Count,
                TodayCount = normalizedRows.Count(x => x.IsToday),
                OverdueCount = normalizedRows.Count(x => x.IsOverdue),
                UnassignedCount = normalizedRows.Count(x => !x.HasAssignedTechnician),
                Reminder24hPendingCount = normalizedRows.Count(x => x.Reminder24hPending),
                Reminder2hPendingCount = normalizedRows.Count(x => x.Reminder2hPending),
                CompletedCount = completedRows.Count,
                OnTimeRate = onTimeRate
            };

            model.StatusFilterOptions = StatusOptions.Select(x => new SelectListItem(x, x)).ToList();
            model.TechnicianFilterOptions = new List<SelectListItem> { new("Todos", AllValue), new("Sin asignar", UnassignedValue) };
            model.TechnicianFilterOptions.AddRange(technicians.Select(x => new SelectListItem(x, x)));
            model.SiteFilterOptions = new List<SelectListItem> { new("Todos", AllValue) };
            model.SiteFilterOptions.AddRange(SiteOptions.Select(x => new SelectListItem(x, x)));

            return View(model);
        }

        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Create()
        {
            var model = new MaintenanceAppointment
            {
                ScheduledFor = DateTime.Now,
                Status = "Programada"
            };

            var metadata = new MaintenanceAppointmentMetadata
            {
                WorkshopEntryAt = model.ScheduledFor,
                EstimatedDurationHours = 2m
            };

            await PopulateFormOptionsAsync(model, metadata, string.Empty);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Create(
            MaintenanceAppointment appointment,
            string? submitAction,
            string? maintenanceOrderNumber,
            decimal? estimatedDurationHours,
            DateTime? workshopEntryAt,
            DateTime? workshopExitAt,
            bool checklistInspectionCompleted,
            bool checklistSparePartsCompleted,
            bool checklistFinalTestCompleted,
            bool checklistUserConformityCompleted,
            string? completionEvidenceNotes)
        {
            ModelState.Remove(nameof(MaintenanceAppointment.AppointmentNumber));
            ModelState.Remove(nameof(MaintenanceAppointment.CreatedAt));
            ModelState.Remove(nameof(MaintenanceAppointment.RowVersion));
            RehydrateDateAndMetadataInputsFromRequest(
                appointment,
                ref estimatedDurationHours,
                ref workshopEntryAt,
                ref workshopExitAt,
                ref checklistInspectionCompleted,
                ref checklistSparePartsCompleted,
                ref checklistFinalTestCompleted,
                ref checklistUserConformityCompleted,
                ref completionEvidenceNotes);

            NormalizeAppointmentInput(appointment);

            var metadata = BuildMetadataInput(
                maintenanceOrderNumber,
                estimatedDurationHours,
                workshopEntryAt,
                workshopExitAt,
                checklistInspectionCompleted,
                checklistSparePartsCompleted,
                checklistFinalTestCompleted,
                checklistUserConformityCompleted,
                completionEvidenceNotes,
                null);

            await ValidateAppointmentBusinessRulesAsync(appointment, metadata, isEdit: false, originalAssignedTechnician: null);

            if (!ModelState.IsValid)
            {
                await PopulateFormOptionsAsync(appointment, metadata, appointment.Notes ?? string.Empty);
                return View(appointment);
            }

            appointment.Site = SiteOptions.First(x => x.Equals(appointment.Site, StringComparison.OrdinalIgnoreCase));
            appointment.Tenencia = TenenciaOptions.First(x => x.Equals(appointment.Tenencia, StringComparison.OrdinalIgnoreCase));
            appointment.MaintenanceType = TypeOptions.First(x => x.Equals(appointment.MaintenanceType, StringComparison.OrdinalIgnoreCase));
            var selectedTechnician = await ResolveMaintenanceTechnicianAsync(appointment.TechnicianCategory, appointment.AssignedTechnician);
            if (selectedTechnician != null)
            {
                appointment.TechnicianCategory = selectedTechnician.Category;
                appointment.AssignedTechnician = selectedTechnician.FullName;
            }
            appointment.Status = NormalizeStatusValue(appointment.Status);
            appointment.CreatedAt = DateTime.UtcNow;
            appointment.AppointmentNumber = await GenerateAppointmentNumberAsync();
            appointment.Notes = MaintenanceAppointmentMetadataHelper.Build(metadata, appointment.Notes);

            _context.MaintenanceAppointments.Add(appointment);
            await _context.SaveChangesAsync();

            try
            {
                var createdMutation = new List<AppointmentAuditMutation>
                {
                    new("Creacion", null, "Cita creada", null)
                };
                await AppendMaintenanceAppointmentAuditAsync(appointment.Id, createdMutation, GetChangedBy());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo registrar auditoria de creacion para la cita {AppointmentId}.", appointment.Id);
            }

            try
            {
                await _emailNotificationService.NotifyMaintenanceAppointmentCreatedAsync(appointment, User.Identity?.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar notificacion de nueva cita {AppointmentNumber}.", appointment.AppointmentNumber);
            }

            if (string.Equals(submitAction, "save_pdf", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(DownloadPdf), new { id = appointment.Id });
            }

            return RedirectToAction(nameof(Details), new { id = appointment.Id });
        }

        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Edit(int id)
        {
            var appointment = await _context.MaintenanceAppointments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var (metadata, userNotes) = MaintenanceAppointmentMetadataHelper.Parse(appointment.Notes);
            appointment.Status = NormalizeStatusValue(appointment.Status);
            appointment.Notes = userNotes;

            await PopulateFormOptionsAsync(appointment, metadata, userNotes);
            return View("Create", appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Edit(
            int id,
            MaintenanceAppointment appointment,
            string? submitAction,
            string? maintenanceOrderNumber,
            decimal? estimatedDurationHours,
            DateTime? workshopEntryAt,
            DateTime? workshopExitAt,
            bool checklistInspectionCompleted,
            bool checklistSparePartsCompleted,
            bool checklistFinalTestCompleted,
            bool checklistUserConformityCompleted,
            string? completionEvidenceNotes,
            string? scheduleChangeComment)
        {
            if (id != appointment.Id)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(MaintenanceAppointment.AppointmentNumber));
            ModelState.Remove(nameof(MaintenanceAppointment.CreatedAt));

            var dbAppointment = await _context.MaintenanceAppointments.FirstOrDefaultAsync(x => x.Id == id);
            if (dbAppointment == null)
            {
                return NotFound();
            }

            RehydrateDateAndMetadataInputsFromRequest(
                appointment,
                ref estimatedDurationHours,
                ref workshopEntryAt,
                ref workshopExitAt,
                ref checklistInspectionCompleted,
                ref checklistSparePartsCompleted,
                ref checklistFinalTestCompleted,
                ref checklistUserConformityCompleted,
                ref completionEvidenceNotes);

            if (appointment.RowVersion is { Length: > 0 })
            {
                _context.Entry(dbAppointment).Property(x => x.RowVersion).OriginalValue = appointment.RowVersion;
            }

            NormalizeAppointmentInput(appointment);
            var (existingMeta, existingUserNotes) = MaintenanceAppointmentMetadataHelper.Parse(dbAppointment.Notes);

            var metadata = BuildMetadataInput(
                maintenanceOrderNumber,
                estimatedDurationHours,
                workshopEntryAt,
                workshopExitAt,
                checklistInspectionCompleted,
                checklistSparePartsCompleted,
                checklistFinalTestCompleted,
                checklistUserConformityCompleted,
                completionEvidenceNotes,
                existingMeta);

            var scheduleChanged = HasScheduledForChange(dbAppointment.ScheduledFor, appointment.ScheduledFor);
            if (scheduleChanged && string.IsNullOrWhiteSpace(scheduleChangeComment))
            {
                ModelState.AddModelError("ScheduleChangeComment", "Si cambias la fecha/hora de la cita debes detallar el motivo del cambio.");
            }

            var auditMutations = BuildAppointmentAuditMutations(
                dbAppointment,
                existingMeta,
                existingUserNotes,
                appointment,
                metadata,
                appointment.Notes ?? string.Empty,
                scheduleChangeComment);

            await ValidateAppointmentBusinessRulesAsync(
                appointment,
                metadata,
                isEdit: true,
                originalAssignedTechnician: dbAppointment.AssignedTechnician);

            if (!ModelState.IsValid)
            {
                await PopulateFormOptionsAsync(appointment, metadata, appointment.Notes ?? string.Empty, scheduleChangeComment);
                return View("Create", appointment);
            }

            dbAppointment.ScheduledFor = appointment.ScheduledFor;
            dbAppointment.RequestingUser = appointment.RequestingUser;
            dbAppointment.Site = SiteOptions.First(x => x.Equals(appointment.Site, StringComparison.OrdinalIgnoreCase));
            dbAppointment.Tenencia = TenenciaOptions.First(x => x.Equals(appointment.Tenencia, StringComparison.OrdinalIgnoreCase));
            dbAppointment.MaintenanceType = TypeOptions.First(x => x.Equals(appointment.MaintenanceType, StringComparison.OrdinalIgnoreCase));
            dbAppointment.AssetOrArea = appointment.AssetOrArea;
            var selectedTechnician = await ResolveMaintenanceTechnicianAsync(appointment.TechnicianCategory, appointment.AssignedTechnician);
            dbAppointment.TechnicianCategory = selectedTechnician?.Category ?? appointment.TechnicianCategory;
            dbAppointment.AssignedTechnician = selectedTechnician?.FullName ?? appointment.AssignedTechnician;
            dbAppointment.RecipientUsers = appointment.RecipientUsers;
            dbAppointment.Description = appointment.Description;
            dbAppointment.Status = NormalizeStatusValue(appointment.Status);
            dbAppointment.Notes = MaintenanceAppointmentMetadataHelper.Build(metadata, appointment.Notes);

            try
            {
                await _context.SaveChangesAsync();

                if (auditMutations.Count > 0)
                {
                    try
                    {
                        await AppendMaintenanceAppointmentAuditAsync(dbAppointment.Id, auditMutations, GetChangedBy());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo registrar auditoria de cambios para la cita {AppointmentId}.", dbAppointment.Id);
                    }
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "La cita fue actualizada por otro usuario. Recargue y vuelva a intentar.");
                appointment.RowVersion = dbAppointment.RowVersion;
                await PopulateFormOptionsAsync(appointment, metadata, appointment.Notes ?? string.Empty, scheduleChangeComment);
                return View("Create", appointment);
            }

            if (string.Equals(submitAction, "save_pdf", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(DownloadPdf), new { id = dbAppointment.Id });
            }

            return RedirectToAction(nameof(Details), new { id = dbAppointment.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var appointment = await _context.MaintenanceAppointments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var (metadata, userNotes) = MaintenanceAppointmentMetadataHelper.Parse(appointment.Notes);
            ViewBag.Metadata = metadata;
            ViewBag.UserNotes = userNotes;
            ViewBag.LinkedTicketId = await ResolveLinkedTicketIdAsync(metadata.MaintenanceOrderNumber);
            ViewBag.StatusLabelClass = GetStatusBadgeClass(NormalizeStatusValue(appointment.Status));
            try
            {
                ViewBag.AuditEntries = await GetMaintenanceAppointmentAuditEntriesAsync(appointment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo cargar el historial auditable de la cita {AppointmentId}.", appointment.Id);
                ViewBag.AuditEntries = new List<MaintenanceAppointmentAuditEntryViewModel>();
            }

            return View(appointment);
        }

        public async Task<IActionResult> DownloadPdf(int id)
        {
            var appointment = await _context.MaintenanceAppointments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var bytes = MaintenanceAppointmentPdfReportService.GeneratePdf(appointment, _environment.WebRootPath);
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return File(bytes, "application/pdf", $"CitaMantenimiento_{appointment.AppointmentNumber}_{suffix}.pdf");
        }

        private async Task PopulateFormOptionsAsync(
            MaintenanceAppointment appointment,
            MaintenanceAppointmentMetadata metadata,
            string userNotes,
            string? scheduleChangeComment = null)
        {
            var users = await _context.Users
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => x.FullName)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var techniciansCatalog = await _context.MaintenanceTechnicians
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Category)
                .ThenBy(x => x.FullName)
                .Select(x => new
                {
                    x.FullName,
                    x.Category,
                    x.Shift,
                    x.MaxActiveOrders,
                    x.IsAvailable,
                    x.IsActive
                })
                .ToListAsync();

            var technicianNames = techniciansCatalog
                .Select(x => x.FullName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var activeByTech = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (technicianNames.Count > 0)
            {
                var activeOrderRows = await _context.Tickets
                    .AsNoTracking()
                    .Where(x =>
                        x.Department == "Mantenimiento" &&
                        x.Status != TicketStatus.Closed &&
                        x.AssignedTechnician != null &&
                        technicianNames.Contains(x.AssignedTechnician))
                    .GroupBy(x => x.AssignedTechnician!)
                    .Select(g => new
                    {
                        Technician = g.Key,
                        ActiveOrders = g.Count()
                    })
                    .ToListAsync();

                activeByTech = activeOrderRows.ToDictionary(
                    x => x.Technician,
                    x => x.ActiveOrders,
                    StringComparer.OrdinalIgnoreCase);
            }

            var categoryOptions = MaintenanceTechnicianCatalog.BuildCategoryOptions(
                techniciansCatalog.Select(x => x.Category),
                appointment.TechnicianCategory);

            var selectableTechnicians = techniciansCatalog
                .Select(x =>
                {
                    var active = activeByTech.TryGetValue(x.FullName, out var count) ? count : 0;
                    var capacity = Math.Max(1, x.MaxActiveOrders);
                    var overCapacity = active >= capacity;
                    var label = $"{x.FullName} ({active}/{capacity}) - {x.Shift}";
                    if (!x.IsAvailable)
                    {
                        label += " - No disponible";
                    }
                    if (overCapacity)
                    {
                        label += " - Capacidad completa";
                    }

                    return new
                    {
                        x.FullName,
                        x.Category,
                        x.IsAvailable,
                        IsOverCapacity = overCapacity,
                        Label = label
                    };
                })
                .DistinctBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.FullName)
                .ToList();

            var technicianCatalogPayload = techniciansCatalog
                .Select(x =>
                {
                    var active = activeByTech.TryGetValue(x.FullName, out var count) ? count : 0;
                    var capacity = Math.Max(1, x.MaxActiveOrders);
                    return new
                    {
                        fullName = x.FullName,
                        category = x.Category,
                        shift = x.Shift,
                        maxActiveOrders = capacity,
                        currentActiveOrders = active,
                        isOverCapacity = active >= capacity,
                        isAvailable = x.IsAvailable,
                        isActive = x.IsActive
                    };
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(appointment.AssignedTechnician) &&
                selectableTechnicians.All(x => !x.FullName.Equals(appointment.AssignedTechnician, StringComparison.OrdinalIgnoreCase)))
            {
                selectableTechnicians.Insert(0, new
                {
                    FullName = appointment.AssignedTechnician,
                    Category = appointment.TechnicianCategory ?? "Sin categoria",
                    IsAvailable = false,
                    IsOverCapacity = false,
                    Label = $"{appointment.AssignedTechnician} (No disponible)"
                });
            }

            var orderNumbers = await _context.Tickets
                .AsNoTracking()
                .Where(x => x.Department == "Mantenimiento" && !string.IsNullOrWhiteSpace(x.TicketNumber))
                .GroupBy(x => x.TicketNumber!)
                .Select(g => new
                {
                    TicketNumber = g.Key,
                    LastCreated = g.Max(x => x.CreatedDate)
                })
                .OrderByDescending(x => x.LastCreated)
                .Select(x => x.TicketNumber)
                .Take(300)
                .ToListAsync();

            ViewBag.RequestingUsers = users.Select(x => new SelectListItem(x, x)).ToList();
            ViewBag.TechnicianCategoryOptions = categoryOptions.Select(x => new SelectListItem(x, x)).ToList();
            ViewBag.Technicians = selectableTechnicians
                .Select(x => new SelectListItem(x.Label, x.FullName))
                .ToList();
            ViewBag.MaintenanceTechnicianCatalogJson = JsonSerializer.Serialize(technicianCatalogPayload);
            ViewBag.SiteOptions = SiteOptions.Select(x => new SelectListItem(x, x)).ToList();
            ViewBag.TenenciaOptions = TenenciaOptions.Select(x => new SelectListItem(x, x)).ToList();
            ViewBag.TypeOptions = TypeOptions.Select(x => new SelectListItem(x, x)).ToList();
            ViewBag.StatusOptions = StatusOptions.Select(x => new SelectListItem(x, x)).ToList();
            ViewBag.OrderOptions = orderNumbers.Select(x => new SelectListItem(x, x)).ToList();

            ViewBag.TemplateOptions = TemplatePresets
                .Select(x => new SelectListItem(x.Value.Label, x.Key))
                .OrderBy(x => x.Text)
                .ToList();

            var templatePayload = TemplatePresets.ToDictionary(
                x => x.Key,
                x => new
                {
                    label = x.Value.Label,
                    description = x.Value.Description,
                    durationHours = x.Value.DurationHours,
                    maintenanceType = x.Value.MaintenanceType
                });

            ViewBag.TemplatePresetsJson = JsonSerializer.Serialize(templatePayload);
            ViewBag.Metadata = metadata;
            ViewBag.UserNotes = userNotes;
            ViewBag.ScheduleChangeComment = scheduleChangeComment;

            if (appointment.ScheduledFor == default)
            {
                appointment.ScheduledFor = DateTime.Now;
            }
        }

        private static MaintenanceAppointmentMetadata BuildMetadataInput(
            string? maintenanceOrderNumber,
            decimal? estimatedDurationHours,
            DateTime? workshopEntryAt,
            DateTime? workshopExitAt,
            bool checklistInspectionCompleted,
            bool checklistSparePartsCompleted,
            bool checklistFinalTestCompleted,
            bool checklistUserConformityCompleted,
            string? completionEvidenceNotes,
            MaintenanceAppointmentMetadata? existingMetadata)
        {
            return new MaintenanceAppointmentMetadata
            {
                MaintenanceOrderNumber = string.IsNullOrWhiteSpace(maintenanceOrderNumber) ? null : maintenanceOrderNumber.Trim(),
                EstimatedDurationHours = estimatedDurationHours,
                WorkshopEntryAt = workshopEntryAt,
                WorkshopExitAt = workshopExitAt,
                ChecklistInspectionCompleted = checklistInspectionCompleted,
                ChecklistSparePartsCompleted = checklistSparePartsCompleted,
                ChecklistFinalTestCompleted = checklistFinalTestCompleted,
                ChecklistUserConformityCompleted = checklistUserConformityCompleted,
                CompletionEvidenceNotes = string.IsNullOrWhiteSpace(completionEvidenceNotes) ? null : completionEvidenceNotes.Trim(),
                Reminder24hSentAtUtc = existingMetadata?.Reminder24hSentAtUtc,
                Reminder2hSentAtUtc = existingMetadata?.Reminder2hSentAtUtc
            };
        }

        private void RehydrateDateAndMetadataInputsFromRequest(
            MaintenanceAppointment appointment,
            ref decimal? estimatedDurationHours,
            ref DateTime? workshopEntryAt,
            ref DateTime? workshopExitAt,
            ref bool checklistInspectionCompleted,
            ref bool checklistSparePartsCompleted,
            ref bool checklistFinalTestCompleted,
            ref bool checklistUserConformityCompleted,
            ref string? completionEvidenceNotes)
        {
            if (!Request.HasFormContentType)
                return;

            var form = Request.Form;

            if (TryReadFormValue(form, nameof(MaintenanceAppointment.ScheduledFor), out var scheduledRaw) &&
                TryParseFlexibleDateTime(scheduledRaw, out var scheduledFor))
            {
                appointment.ScheduledFor = scheduledFor;
                ModelState.Remove(nameof(MaintenanceAppointment.ScheduledFor));
            }

            if (TryReadFormValue(form, "estimatedDurationHours", out var durationRaw))
            {
                estimatedDurationHours = TryParseFlexibleDecimal(durationRaw, out var durationParsed)
                    ? durationParsed
                    : null;
                ModelState.Remove("estimatedDurationHours");
            }

            if (TryReadFormValue(form, "workshopEntryAt", out var entryRaw))
            {
                workshopEntryAt = TryParseFlexibleDateTime(entryRaw, out var entryParsed)
                    ? entryParsed
                    : null;
                ModelState.Remove("workshopEntryAt");
            }

            if (TryReadFormValue(form, "workshopExitAt", out var exitRaw))
            {
                workshopExitAt = TryParseFlexibleDateTime(exitRaw, out var exitParsed)
                    ? exitParsed
                    : null;
                ModelState.Remove("workshopExitAt");
            }

            checklistInspectionCompleted = ReadCheckbox(form, "checklistInspectionCompleted");
            checklistSparePartsCompleted = ReadCheckbox(form, "checklistSparePartsCompleted");
            checklistFinalTestCompleted = ReadCheckbox(form, "checklistFinalTestCompleted");
            checklistUserConformityCompleted = ReadCheckbox(form, "checklistUserConformityCompleted");

            if (TryReadFormValue(form, "completionEvidenceNotes", out var evidenceRaw))
            {
                completionEvidenceNotes = string.IsNullOrWhiteSpace(evidenceRaw) ? null : evidenceRaw.Trim();
                ModelState.Remove("completionEvidenceNotes");
            }
        }

        private static bool TryReadFormValue(Microsoft.AspNetCore.Http.IFormCollection form, string key, out string value)
        {
            value = string.Empty;
            if (!form.TryGetValue(key, out var raw) || raw.Count == 0)
                return false;

            value = raw[0] ?? string.Empty;
            return true;
        }

        private static bool ReadCheckbox(Microsoft.AspNetCore.Http.IFormCollection form, string key)
        {
            if (!form.TryGetValue(key, out var values))
                return false;

            foreach (var item in values)
            {
                if (string.Equals(item, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item, "on", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item, "1", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseFlexibleDecimal(string? raw, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var text = raw.Trim();
            var styles = NumberStyles.Number | NumberStyles.AllowDecimalPoint;

            return decimal.TryParse(text, styles, CultureInfo.CurrentCulture, out value) ||
                   decimal.TryParse(text, styles, new CultureInfo("es-NI"), out value) ||
                   decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseFlexibleDateTime(string? raw, out DateTime value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var text = raw.Trim();
            var formats = new[]
            {
                "yyyy-MM-ddTHH:mm",
                "yyyy-MM-dd HH:mm",
                "dd/MM/yyyy HH:mm",
                "dd/MM/yyyy hh:mm tt",
                "d/M/yyyy HH:mm",
                "d/M/yyyy hh:mm tt"
            };

            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value))
                return true;

            if (DateTime.TryParseExact(text, formats, new CultureInfo("es-NI"), DateTimeStyles.AssumeLocal, out value))
                return true;

            if (DateTime.TryParse(text, new CultureInfo("es-NI"), DateTimeStyles.AssumeLocal, out value))
                return true;

            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out value))
                return true;

            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value);
        }

        private static bool HasScheduledForChange(DateTime original, DateTime updated)
        {
            return TrimToMinute(original) != TrimToMinute(updated);
        }

        private static DateTime TrimToMinute(DateTime value)
        {
            return value.AddSeconds(-value.Second).AddMilliseconds(-value.Millisecond);
        }

        private static List<AppointmentAuditMutation> BuildAppointmentAuditMutations(
            MaintenanceAppointment original,
            MaintenanceAppointmentMetadata originalMeta,
            string originalUserNotes,
            MaintenanceAppointment updated,
            MaintenanceAppointmentMetadata updatedMeta,
            string updatedUserNotes,
            string? scheduleChangeComment)
        {
            var changes = new List<AppointmentAuditMutation>();

            AddAuditMutation(
                changes,
                ScheduleFieldKey,
                FormatDateTimeAudit(original.ScheduledFor),
                FormatDateTimeAudit(updated.ScheduledFor),
                string.IsNullOrWhiteSpace(scheduleChangeComment) ? null : scheduleChangeComment.Trim(),
                compareByMinute: true);

            AddAuditMutation(changes, "Estado", NormalizeText(original.Status), NormalizeText(updated.Status));
            AddAuditMutation(changes, "Sitio", NormalizeText(original.Site), NormalizeText(updated.Site));
            AddAuditMutation(changes, "Tenencia", NormalizeText(original.Tenencia), NormalizeText(updated.Tenencia));
            AddAuditMutation(changes, "Tipo de mantenimiento", NormalizeText(original.MaintenanceType), NormalizeText(updated.MaintenanceType));
            AddAuditMutation(changes, "Activo / Area", NormalizeText(original.AssetOrArea), NormalizeText(updated.AssetOrArea));
            AddAuditMutation(changes, "Solicitante", NormalizeText(original.RequestingUser), NormalizeText(updated.RequestingUser));
            AddAuditMutation(changes, "Categoria tecnico", NormalizeText(original.TechnicianCategory), NormalizeText(updated.TechnicianCategory));
            AddAuditMutation(changes, "Tecnico asignado", NormalizeText(original.AssignedTechnician), NormalizeText(updated.AssignedTechnician));
            AddAuditMutation(changes, "Usuarios destino", NormalizeText(original.RecipientUsers), NormalizeText(updated.RecipientUsers));
            AddAuditMutation(changes, "Descripcion", NormalizeText(original.Description), NormalizeText(updated.Description));
            AddAuditMutation(changes, "Notas", NormalizeText(originalUserNotes), NormalizeText(updatedUserNotes));

            AddAuditMutation(changes, "Orden relacionada", NormalizeText(originalMeta.MaintenanceOrderNumber), NormalizeText(updatedMeta.MaintenanceOrderNumber));
            AddAuditMutation(changes, "Duracion estimada (h)", FormatDecimalAudit(originalMeta.EstimatedDurationHours), FormatDecimalAudit(updatedMeta.EstimatedDurationHours));
            AddAuditMutation(changes, "Ingreso taller", FormatDateTimeAudit(originalMeta.WorkshopEntryAt), FormatDateTimeAudit(updatedMeta.WorkshopEntryAt));
            AddAuditMutation(changes, "Salida taller", FormatDateTimeAudit(originalMeta.WorkshopExitAt), FormatDateTimeAudit(updatedMeta.WorkshopExitAt));
            AddAuditMutation(changes, "Checklist: inspeccion inicial", FormatBoolAudit(originalMeta.ChecklistInspectionCompleted), FormatBoolAudit(updatedMeta.ChecklistInspectionCompleted));
            AddAuditMutation(changes, "Checklist: repuestos validados", FormatBoolAudit(originalMeta.ChecklistSparePartsCompleted), FormatBoolAudit(updatedMeta.ChecklistSparePartsCompleted));
            AddAuditMutation(changes, "Checklist: prueba final", FormatBoolAudit(originalMeta.ChecklistFinalTestCompleted), FormatBoolAudit(updatedMeta.ChecklistFinalTestCompleted));
            AddAuditMutation(changes, "Checklist: conformidad usuario", FormatBoolAudit(originalMeta.ChecklistUserConformityCompleted), FormatBoolAudit(updatedMeta.ChecklistUserConformityCompleted));
            AddAuditMutation(changes, "Evidencia de cierre", NormalizeText(originalMeta.CompletionEvidenceNotes), NormalizeText(updatedMeta.CompletionEvidenceNotes));

            return changes;
        }

        private static void AddAuditMutation(
            List<AppointmentAuditMutation> changes,
            string field,
            string? oldValue,
            string? newValue,
            string? reason = null,
            bool compareByMinute = false)
        {
            if (compareByMinute &&
                DateTime.TryParse(oldValue, out var oldDate) &&
                DateTime.TryParse(newValue, out var newDate) &&
                !HasScheduledForChange(oldDate, newDate))
            {
                return;
            }

            if (string.Equals(NormalizeText(oldValue), NormalizeText(newValue), StringComparison.OrdinalIgnoreCase))
                return;

            changes.Add(new AppointmentAuditMutation(
                field,
                NormalizeAuditValue(oldValue),
                NormalizeAuditValue(newValue),
                NormalizeAuditValue(reason)));
        }

        private static string? NormalizeAuditValue(string? value)
        {
            var normalized = NormalizeText(value);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            return normalized.Length > 1000 ? normalized[..1000] : normalized;
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string FormatDateTimeAudit(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string FormatDecimalAudit(decimal? value)
        {
            return value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string FormatBoolAudit(bool value)
        {
            return value ? "Si" : "No";
        }

        private async Task EnsureMaintenanceAppointmentAuditTableAsync()
        {
            var sql = $@"
IF OBJECT_ID(N'{AuditTableName}', N'U') IS NULL
BEGIN
    CREATE TABLE {AuditTableName}(
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_MaintenanceAppointmentAudits] PRIMARY KEY,
        [MaintenanceAppointmentId] INT NOT NULL,
        [ChangeDateUtc] DATETIME2 NOT NULL CONSTRAINT [DF_MaintenanceAppointmentAudits_ChangeDateUtc] DEFAULT SYSUTCDATETIME(),
        [FieldChanged] NVARCHAR(120) NOT NULL,
        [OldValue] NVARCHAR(1000) NULL,
        [NewValue] NVARCHAR(1000) NULL,
        [ChangedBy] NVARCHAR(150) NULL,
        [ChangeReason] NVARCHAR(1000) NULL,
        CONSTRAINT [FK_MaintenanceAppointmentAudits_MaintenanceAppointments]
            FOREIGN KEY([MaintenanceAppointmentId]) REFERENCES [dbo].[MaintenanceAppointments]([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_MaintenanceAppointmentAudits_AppointmentId_ChangeDate]
        ON {AuditTableName}([MaintenanceAppointmentId], [ChangeDateUtc] DESC, [Id] DESC);
END";

            await _context.Database.ExecuteSqlRawAsync(sql);
        }

        private async Task AppendMaintenanceAppointmentAuditAsync(
            int appointmentId,
            IReadOnlyCollection<AppointmentAuditMutation> mutations,
            string changedBy)
        {
            if (mutations.Count == 0)
                return;

            await EnsureMaintenanceAppointmentAuditTableAsync();

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync();

            try
            {
                foreach (var mutation in mutations)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = $@"
INSERT INTO {AuditTableName}
    ([MaintenanceAppointmentId], [FieldChanged], [OldValue], [NewValue], [ChangedBy], [ChangeReason])
VALUES
    (@appointmentId, @field, @oldValue, @newValue, @changedBy, @changeReason);";

                    AddDbParameter(command, "@appointmentId", appointmentId);
                    AddDbParameter(command, "@field", mutation.Field);
                    AddDbParameter(command, "@oldValue", mutation.OldValue);
                    AddDbParameter(command, "@newValue", mutation.NewValue);
                    AddDbParameter(command, "@changedBy", changedBy);
                    AddDbParameter(command, "@changeReason", mutation.Reason);

                    await command.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }
        }

        private async Task<List<MaintenanceAppointmentAuditEntryViewModel>> GetMaintenanceAppointmentAuditEntriesAsync(int appointmentId)
        {
            await EnsureMaintenanceAppointmentAuditTableAsync();

            var rows = new List<MaintenanceAppointmentAuditEntryViewModel>();
            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync();

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $@"
SELECT [Id], [ChangeDateUtc], [FieldChanged], [OldValue], [NewValue], [ChangedBy], [ChangeReason]
FROM {AuditTableName}
WHERE [MaintenanceAppointmentId] = @appointmentId
ORDER BY [ChangeDateUtc] DESC, [Id] DESC;";
                AddDbParameter(command, "@appointmentId", appointmentId);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var utcRaw = reader.GetDateTime(1);
                    var utc = utcRaw.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(utcRaw, DateTimeKind.Utc)
                        : utcRaw.ToUniversalTime();

                    rows.Add(new MaintenanceAppointmentAuditEntryViewModel
                    {
                        Id = reader.GetInt32(0),
                        ChangeDateLocal = utc.ToLocalTime(),
                        FieldChanged = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        OldValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                        NewValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                        ChangedBy = reader.IsDBNull(5) ? "Sistema" : reader.GetString(5),
                        ChangeReason = reader.IsDBNull(6) ? null : reader.GetString(6)
                    });
                }
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }

            return rows;
        }

        private static void AddDbParameter(System.Data.Common.DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private string GetChangedBy()
        {
            var raw = User?.Identity?.Name;
            return string.IsNullOrWhiteSpace(raw) ? "Sistema" : raw.Trim();
        }

        private async Task ValidateAppointmentBusinessRulesAsync(
            MaintenanceAppointment appointment,
            MaintenanceAppointmentMetadata metadata,
            bool isEdit,
            string? originalAssignedTechnician)
        {
            if (string.IsNullOrWhiteSpace(appointment.RequestingUser))
                ModelState.AddModelError(nameof(MaintenanceAppointment.RequestingUser), "Seleccione el solicitante.");

            if (string.IsNullOrWhiteSpace(appointment.TechnicianCategory))
                ModelState.AddModelError(nameof(MaintenanceAppointment.TechnicianCategory), "Seleccione la categoria del tecnico.");

            if (string.IsNullOrWhiteSpace(appointment.AssignedTechnician))
                ModelState.AddModelError(nameof(MaintenanceAppointment.AssignedTechnician), "Seleccione un tecnico asignado.");
            else if (!string.IsNullOrWhiteSpace(appointment.TechnicianCategory))
            {
                var isSameAssigned = !string.IsNullOrWhiteSpace(originalAssignedTechnician) &&
                                     string.Equals(originalAssignedTechnician, appointment.AssignedTechnician, StringComparison.OrdinalIgnoreCase);

                var selectedTechnician = await _context.MaintenanceTechnicians
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.IsActive &&
                        x.Category == appointment.TechnicianCategory &&
                        x.FullName == appointment.AssignedTechnician &&
                        (x.IsAvailable || isSameAssigned));

                if (selectedTechnician == null)
                {
                    ModelState.AddModelError(nameof(MaintenanceAppointment.AssignedTechnician), "El tecnico seleccionado no esta disponible en la categoria indicada.");
                }
                else if (!isSameAssigned)
                {
                    var activeOrders = await _context.Tickets
                        .AsNoTracking()
                        .CountAsync(x =>
                            x.Department == "Mantenimiento" &&
                            x.Status != TicketStatus.Closed &&
                            x.AssignedTechnician == selectedTechnician.FullName);

                    if (activeOrders >= Math.Max(1, selectedTechnician.MaxActiveOrders))
                    {
                        ModelState.AddModelError(nameof(MaintenanceAppointment.AssignedTechnician), $"El tecnico ya alcanzo su capacidad maxima ({selectedTechnician.MaxActiveOrders} ordenes activas).");
                    }
                }
            }

            if (!SiteOptions.Contains(appointment.Site, StringComparer.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(MaintenanceAppointment.Site), "Seleccione un sitio valido.");

            if (!TenenciaOptions.Contains(appointment.Tenencia, StringComparer.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(MaintenanceAppointment.Tenencia), "Seleccione una tenencia valida.");

            if (!TypeOptions.Contains(appointment.MaintenanceType, StringComparer.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(MaintenanceAppointment.MaintenanceType), "Seleccione un tipo de mantenimiento valido.");

            var normalizedStatus = NormalizeStatusValue(appointment.Status);
            if (!StatusOptions.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(MaintenanceAppointment.Status), "Seleccione un estado valido.");

            if (!isEdit && appointment.ScheduledFor < DateTime.Now.AddMinutes(-5))
                ModelState.AddModelError(nameof(MaintenanceAppointment.ScheduledFor), "La cita no puede programarse en una fecha pasada.");

            if (string.IsNullOrWhiteSpace(appointment.AssetOrArea))
                ModelState.AddModelError(nameof(MaintenanceAppointment.AssetOrArea), "Ingrese la unidad/equipo o area.");

            if (metadata.EstimatedDurationHours == null || metadata.EstimatedDurationHours <= 0)
                ModelState.AddModelError("EstimatedDurationHours", "Ingrese la duracion estimada (horas).");

            if (metadata.EstimatedDurationHours > 72)
                ModelState.AddModelError("EstimatedDurationHours", "La duracion estimada no puede ser mayor a 72 horas.");

            if (metadata.WorkshopEntryAt.HasValue && metadata.WorkshopExitAt.HasValue &&
                metadata.WorkshopExitAt.Value < metadata.WorkshopEntryAt.Value)
            {
                ModelState.AddModelError("WorkshopExitAt", "La salida de taller no puede ser menor a la entrada.");
            }

            if (!string.IsNullOrWhiteSpace(metadata.MaintenanceOrderNumber))
            {
                var exists = await _context.Tickets
                    .AsNoTracking()
                    .AnyAsync(x => x.Department == "Mantenimiento" && x.TicketNumber == metadata.MaintenanceOrderNumber);

                if (!exists)
                    ModelState.AddModelError("MaintenanceOrderNumber", "La orden de mantenimiento relacionada no existe.");
            }

            if (string.Equals(normalizedStatus, "Completada", StringComparison.OrdinalIgnoreCase))
            {
                if (!metadata.ChecklistInspectionCompleted || !metadata.ChecklistSparePartsCompleted ||
                    !metadata.ChecklistFinalTestCompleted || !metadata.ChecklistUserConformityCompleted)
                {
                    ModelState.AddModelError("ChecklistCompletion", "Para marcar como completada, el checklist tecnico debe estar al 100%.");
                }

                if (string.IsNullOrWhiteSpace(metadata.CompletionEvidenceNotes))
                    ModelState.AddModelError("CompletionEvidenceNotes", "Para cerrar/completar, registre evidencia tecnica minima.");
            }

        }

        private static void NormalizeAppointmentInput(MaintenanceAppointment appointment)
        {
            appointment.RequestingUser = (appointment.RequestingUser ?? string.Empty).Trim();
            appointment.Site = (appointment.Site ?? string.Empty).Trim();
            appointment.Tenencia = (appointment.Tenencia ?? string.Empty).Trim();
            appointment.MaintenanceType = (appointment.MaintenanceType ?? string.Empty).Trim();
            appointment.AssetOrArea = (appointment.AssetOrArea ?? string.Empty).Trim();
            appointment.TechnicianCategory = string.IsNullOrWhiteSpace(appointment.TechnicianCategory) ? null : appointment.TechnicianCategory.Trim();
            appointment.AssignedTechnician = string.IsNullOrWhiteSpace(appointment.AssignedTechnician) ? null : appointment.AssignedTechnician.Trim();
            appointment.RecipientUsers = string.IsNullOrWhiteSpace(appointment.RecipientUsers) ? null : appointment.RecipientUsers.Trim();
            appointment.Description = (appointment.Description ?? string.Empty).Trim();
            appointment.Notes = string.IsNullOrWhiteSpace(appointment.Notes) ? null : appointment.Notes.Trim();
            appointment.Status = string.IsNullOrWhiteSpace(appointment.Status) ? "Programada" : appointment.Status.Trim();
        }

        private async Task EnsureAutomaticRemindersAsync(List<MaintenanceAppointment> appointments, DateTime nowLocal)
        {
            var changed = false;

            foreach (var appointment in appointments)
            {
                var canonicalStatus = NormalizeStatusValue(appointment.Status);
                if (!string.Equals(canonicalStatus, appointment.Status, StringComparison.OrdinalIgnoreCase))
                {
                    appointment.Status = canonicalStatus;
                    changed = true;
                }

                if (IsClosedStatus(canonicalStatus))
                    continue;

                var (meta, userNotes) = MaintenanceAppointmentMetadataHelper.Parse(appointment.Notes);
                var remaining = appointment.ScheduledFor - nowLocal;
                var within24h = remaining.TotalHours <= 24 && remaining.TotalHours > 2;
                var within2h = remaining.TotalHours <= 2 && remaining.TotalMinutes >= -10;
                var reminderChanged = false;

                if (within24h && !meta.Reminder24hSentAtUtc.HasValue)
                {
                    try
                    {
                        await _emailNotificationService.NotifyMaintenanceAppointmentReminderAsync(appointment, "24h");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo enviar recordatorio 24h para {AppointmentNumber}.", appointment.AppointmentNumber);
                    }

                    meta.Reminder24hSentAtUtc = DateTime.UtcNow;
                    reminderChanged = true;
                }

                if (within2h && !meta.Reminder2hSentAtUtc.HasValue)
                {
                    try
                    {
                        await _emailNotificationService.NotifyMaintenanceAppointmentReminderAsync(appointment, "2h");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo enviar recordatorio 2h para {AppointmentNumber}.", appointment.AppointmentNumber);
                    }

                    meta.Reminder2hSentAtUtc = DateTime.UtcNow;
                    reminderChanged = true;
                }

                if (reminderChanged)
                {
                    appointment.Notes = MaintenanceAppointmentMetadataHelper.Build(meta, userNotes);
                    changed = true;
                }
            }

            if (changed)
                await _context.SaveChangesAsync();
        }

        private static MaintenanceAppointmentListItemViewModel BuildListItem(
            MaintenanceAppointment appointment,
            MaintenanceAppointmentMetadata metadata,
            string userNotes,
            DateTime nowLocal)
        {
            var normalizedStatus = NormalizeStatusValue(appointment.Status);
            var isCompleted = string.Equals(normalizedStatus, "Completada", StringComparison.OrdinalIgnoreCase);
            var isCanceled = string.Equals(normalizedStatus, "Cancelada", StringComparison.OrdinalIgnoreCase);
            var isClosed = isCompleted || isCanceled;
            var hasTechnician = !string.IsNullOrWhiteSpace(appointment.AssignedTechnician);

            // Evita marcar "Vencida" de inmediato por diferencias de segundos/minutos al crear la cita.
            var isOverdue = !isClosed && appointment.ScheduledFor < nowLocal.AddMinutes(-5);
            var isToday = appointment.ScheduledFor.Date == nowLocal.Date;
            var isTomorrow = appointment.ScheduledFor.Date == nowLocal.Date.AddDays(1);

            var remaining = appointment.ScheduledFor - nowLocal;
            var reminder24hPending = !isClosed && remaining.TotalHours <= 24 && remaining.TotalHours > 2 && !metadata.Reminder24hSentAtUtc.HasValue;
            var reminder2hPending = !isClosed && remaining.TotalHours <= 2 && remaining.TotalMinutes >= -10 && !metadata.Reminder2hSentAtUtc.HasValue;

            var proximityClass = "secondary";
            var proximityLabel = "Programada";

            if (isCanceled)
            {
                proximityClass = "dark";
                proximityLabel = "Cancelada";
            }
            else if (isCompleted)
            {
                proximityClass = "success";
                proximityLabel = "Completada";
            }
            else if (isOverdue)
            {
                proximityClass = "danger";
                proximityLabel = "Vencida";
            }
            else if (isToday)
            {
                proximityClass = "warning text-dark";
                proximityLabel = "Hoy";
            }
            else if (isTomorrow)
            {
                proximityClass = "info text-dark";
                proximityLabel = "Manana";
            }

            return new MaintenanceAppointmentListItemViewModel
            {
                Appointment = appointment,
                Metadata = metadata,
                UserNotes = userNotes,
                IsOverdue = isOverdue,
                IsToday = isToday,
                IsTomorrow = isTomorrow,
                Reminder24hPending = reminder24hPending,
                Reminder2hPending = reminder2hPending,
                HasAssignedTechnician = hasTechnician,
                IsCompleted = isCompleted,
                ProximityLabel = proximityLabel,
                ProximityClass = proximityClass,
                ChecklistProgressPercent = metadata.GetChecklistProgressPercent()
            };
        }

        private static string NormalizeStatusValue(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "Programada";

            var normalized = status.Trim();
            if (LegacyStatusMap.TryGetValue(normalized, out var mapped))
                return mapped;

            var canonical = StatusOptions.FirstOrDefault(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            return canonical ?? "Programada";
        }

        private static bool IsClosedStatus(string status)
        {
            return string.Equals(status, "Completada", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Cancelada", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<MaintenanceTechnician?> ResolveMaintenanceTechnicianAsync(string? category, string? technicianName)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(technicianName))
                return null;

            var normalizedCategory = category.Trim();
            var normalizedName = technicianName.Trim();

            return await _context.MaintenanceTechnicians
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.IsActive &&
                    x.Category == normalizedCategory &&
                    x.FullName == normalizedName);
        }

        private async Task<int?> ResolveLinkedTicketIdAsync(string? ticketNumber)
        {
            if (string.IsNullOrWhiteSpace(ticketNumber))
                return null;

            return await _context.Tickets
                .AsNoTracking()
                .Where(x => x.Department == "Mantenimiento" && x.TicketNumber == ticketNumber.Trim())
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();
        }

        private static string GetStatusBadgeClass(string normalizedStatus)
        {
            return normalizedStatus switch
            {
                "Programada" => "primary",
                "Confirmada" => "info text-dark",
                "En ejecucion" => "warning text-dark",
                "Completada" => "success",
                "Reprogramada" => "secondary",
                "Cancelada" => "dark",
                _ => "secondary"
            };
        }

        private async Task<string> GenerateAppointmentNumberAsync()
        {
            var prefix = $"CM-{DateTime.UtcNow:yyyyMMdd}";
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);

            var todayCount = await _context.MaintenanceAppointments
                .AsNoTracking()
                .CountAsync(x => x.CreatedAt >= todayStart && x.CreatedAt < todayEnd);

            return $"{prefix}-{(todayCount + 1):D3}";
        }
    }
}
