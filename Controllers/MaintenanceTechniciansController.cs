using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,GerenciaGeneral")]
    public class MaintenanceTechniciansController : Controller
    {
        private const string AvailabilityAll = "all";
        private const string AvailabilityOnlyAvailable = "available";
        private const string AvailabilityOnlyUnavailable = "unavailable";

        private readonly ApplicationDbContext _context;

        public MaintenanceTechniciansController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? search,
            string? category,
            string? availability = AvailabilityAll,
            bool includeInactive = false)
        {
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var normalizedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
            var normalizedAvailability = NormalizeAvailabilityFilter(availability);

            var query = _context.MaintenanceTechnicians.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(x => x.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(x =>
                    x.FullName.Contains(normalizedSearch) ||
                    x.Category.Contains(normalizedSearch) ||
                    x.PhoneNumber.Contains(normalizedSearch) ||
                    x.EmergencyContactName.Contains(normalizedSearch) ||
                    x.EmergencyContactPhone.Contains(normalizedSearch));
            }

            if (!string.IsNullOrWhiteSpace(normalizedCategory))
            {
                query = query.Where(x => x.Category == normalizedCategory);
            }

            query = normalizedAvailability switch
            {
                AvailabilityOnlyAvailable => query.Where(x => x.IsAvailable),
                AvailabilityOnlyUnavailable => query.Where(x => !x.IsAvailable),
                _ => query
            };

            var rows = await query
                .OrderBy(x => x.Category)
                .ThenBy(x => x.FullName)
                .ToListAsync();

            var technicianNames = rows
                .Select(x => x.FullName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var activeLoads = technicianNames.Count == 0
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : (await _context.Tickets
                    .AsNoTracking()
                    .Where(x =>
                        x.Department == "Mantenimiento" &&
                        x.Status != TicketStatus.Closed &&
                        x.AssignedTechnician != null &&
                        technicianNames.Contains(x.AssignedTechnician))
                    .GroupBy(x => x.AssignedTechnician!)
                    .Select(g => new { Technician = g.Key, ActiveOrders = g.Count() })
                    .ToListAsync())
                    .ToDictionary(x => x.Technician, x => x.ActiveOrders, StringComparer.OrdinalIgnoreCase);

            await PopulateOptionsAsync(normalizedCategory, normalizedAvailability, includeInactive);
            ViewBag.Search = normalizedSearch ?? string.Empty;
            ViewBag.TotalTechnicians = rows.Count;
            ViewBag.AvailableCount = rows.Count(x => x.IsAvailable);
            ViewBag.UnavailableCount = rows.Count(x => !x.IsAvailable);
            ViewBag.ActiveLoads = activeLoads;

            return View(rows);
        }

        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Create()
        {
            var categories = await BuildCategoryCatalogAsync();
            var model = new MaintenanceTechnician
            {
                Category = categories.FirstOrDefault() ?? "Mecanico",
                Shift = "Diurno",
                MaxActiveOrders = 4,
                IsAvailable = true,
                IsActive = true
            };

            await PopulateOptionsAsync(model.Category, AvailabilityAll, includeInactive: false);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Create(MaintenanceTechnician model)
        {
            NormalizeModel(model);
            await ValidateModelAsync(model, currentId: null);

            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(model.Category, AvailabilityAll, includeInactive: !model.IsActive);
                return View(model);
            }

            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            _context.MaintenanceTechnicians.Add(model);
            await _context.SaveChangesAsync();

            TempData["TicketsMessage"] = "Tecnico agregado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _context.MaintenanceTechnicians.FirstOrDefaultAsync(x => x.Id == id);
            if (model == null)
            {
                return NotFound();
            }

            await PopulateOptionsAsync(model.Category, AvailabilityAll, includeInactive: !model.IsActive);
            return View("Create", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Edit(int id, MaintenanceTechnician model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (model.RowVersion == null)
            {
                ModelState.AddModelError(string.Empty, "No se pudo validar concurrencia del registro.");
                await PopulateOptionsAsync(model.Category, AvailabilityAll, includeInactive: !model.IsActive);
                return View("Create", model);
            }

            var dbRow = await _context.MaintenanceTechnicians.FirstOrDefaultAsync(x => x.Id == id);
            if (dbRow == null)
            {
                return NotFound();
            }

            NormalizeModel(model);
            await ValidateModelAsync(model, currentId: id);

            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(model.Category, AvailabilityAll, includeInactive: !model.IsActive);
                return View("Create", model);
            }

            _context.Entry(dbRow).Property(x => x.RowVersion).OriginalValue = model.RowVersion;

            dbRow.FullName = model.FullName;
            dbRow.Category = model.Category;
            dbRow.Shift = model.Shift;
            dbRow.MaxActiveOrders = model.MaxActiveOrders;
            dbRow.PhoneNumber = model.PhoneNumber;
            dbRow.EmergencyContactName = model.EmergencyContactName;
            dbRow.EmergencyRelationship = model.EmergencyRelationship;
            dbRow.EmergencyContactPhone = model.EmergencyContactPhone;
            dbRow.IsAvailable = model.IsAvailable;
            dbRow.IsActive = model.IsActive;
            dbRow.Notes = model.Notes;
            dbRow.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "El tecnico fue modificado por otro usuario.");
                model.RowVersion = dbRow.RowVersion;
                await PopulateOptionsAsync(model.Category, AvailabilityAll, includeInactive: !model.IsActive);
                return View("Create", model);
            }

            TempData["TicketsMessage"] = "Tecnico actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> ToggleAvailability(int id, string? returnUrl = null)
        {
            var row = await _context.MaintenanceTechnicians.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (row == null)
            {
                return NotFound();
            }

            row.IsAvailable = !row.IsAvailable;
            row.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Delete(int id)
        {
            var row = await _context.MaintenanceTechnicians.FirstOrDefaultAsync(x => x.Id == id);
            if (row == null)
            {
                return NotFound();
            }

            row.IsActive = false;
            row.IsAvailable = false;
            row.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["TicketsMessage"] = "Tecnico desactivado.";
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateModelAsync(MaintenanceTechnician model, int? currentId)
        {
            var categories = await BuildCategoryCatalogAsync(model.Category);
            if (!categories.Contains(model.Category, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(MaintenanceTechnician.Category), "Seleccione una categoria valida.");
            }

            var shifts = MaintenanceTechnicianCatalog.BuildShiftOptions(model.Shift);
            if (!shifts.Contains(model.Shift, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(MaintenanceTechnician.Shift), "Seleccione un turno valido.");
            }

            var duplicated = await _context.MaintenanceTechnicians
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id != (currentId ?? 0) &&
                    x.FullName == model.FullName &&
                    x.Category == model.Category &&
                    x.IsActive);

            if (duplicated)
            {
                ModelState.AddModelError(nameof(MaintenanceTechnician.FullName), "Ya existe un tecnico activo con ese nombre y categoria.");
            }
        }

        private static string NormalizeAvailabilityFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AvailabilityAll;
            }

            var normalized = value.Trim().ToLowerInvariant();
            return normalized switch
            {
                AvailabilityOnlyAvailable => AvailabilityOnlyAvailable,
                AvailabilityOnlyUnavailable => AvailabilityOnlyUnavailable,
                _ => AvailabilityAll
            };
        }

        private void NormalizeModel(MaintenanceTechnician model)
        {
            model.FullName = (model.FullName ?? string.Empty).Trim();
            model.Category = (model.Category ?? string.Empty).Trim();
            model.Shift = (model.Shift ?? string.Empty).Trim();
            if (model.MaxActiveOrders < 1)
            {
                model.MaxActiveOrders = 1;
            }
            model.PhoneNumber = (model.PhoneNumber ?? string.Empty).Trim();
            model.EmergencyContactName = (model.EmergencyContactName ?? string.Empty).Trim();
            model.EmergencyRelationship = (model.EmergencyRelationship ?? string.Empty).Trim();
            model.EmergencyContactPhone = (model.EmergencyContactPhone ?? string.Empty).Trim();
            model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        }

        private async Task PopulateOptionsAsync(string? selectedCategory, string selectedAvailability, bool includeInactive)
        {
            var categories = await BuildCategoryCatalogAsync(selectedCategory);
            var shifts = MaintenanceTechnicianCatalog.BuildShiftOptions();

            ViewBag.CategoryOptions = categories
                .Select(x => new SelectListItem(x, x, x.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ViewBag.ShiftOptions = shifts
                .Select(x => new SelectListItem(x, x))
                .ToList();

            ViewBag.AvailabilityOptions = new List<SelectListItem>
            {
                new("Todos", AvailabilityAll, selectedAvailability == AvailabilityAll),
                new("Disponibles", AvailabilityOnlyAvailable, selectedAvailability == AvailabilityOnlyAvailable),
                new("No disponibles", AvailabilityOnlyUnavailable, selectedAvailability == AvailabilityOnlyUnavailable)
            };

            ViewBag.IncludeInactive = includeInactive;
        }

        private async Task<List<string>> BuildCategoryCatalogAsync(string? includeValue = null)
        {
            var dynamicCategories = await _context.MaintenanceTechnicians
                .AsNoTracking()
                .Where(x => !string.IsNullOrWhiteSpace(x.Category))
                .Select(x => x.Category)
                .Distinct()
                .ToListAsync();

            return MaintenanceTechnicianCatalog.BuildCategoryOptions(dynamicCategories, includeValue);
        }
    }
}
