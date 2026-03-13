using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public InventoryController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> Index(
            string? search,
            string? category,
            string? status,
            string? site,
            bool includeInactive = false)
        {
            var query = BuildFilteredQuery(search, category, status, site, includeInactive);

            var items = await query
                .OrderBy(i => i.Category)
                .ThenBy(i => i.AssetCode)
                .ToListAsync();

            await PopulateOptionsAsync(category, status, site, null, null, null, includeInactive);
            ViewBag.Search = search;

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var item = await _context.InventoryItems
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Create()
        {
            await PopulateOptionsAsync(null, null, null, null, null, null, false);
            return View(new InventoryItem
            {
                Status = "Disponible",
                Condition = "Bueno",
                IsActive = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Create(InventoryItem item)
        {
            item.AssetCode = (item.AssetCode ?? string.Empty).Trim();
            item.AssetTag = item.AssetTag?.Trim();
            item.SerialNumber = item.SerialNumber?.Trim();
            item.Category = (item.Category ?? string.Empty).Trim();
            item.Subcategory = item.Subcategory?.Trim();
            item.Brand = item.Brand?.Trim();
            item.Model = item.Model?.Trim();
            item.Site = item.Site?.Trim();
            item.Department = item.Department?.Trim();
            item.AssignedTo = item.AssignedTo?.Trim();
            item.AssignedToEmail = item.AssignedToEmail?.Trim();
            item.CorporatePhoneNumber = item.CorporatePhoneNumber?.Trim();
            item.Status = (item.Status ?? string.Empty).Trim();
            item.Condition = (item.Condition ?? string.Empty).Trim();
            item.Supplier = item.Supplier?.Trim();
            item.InvoiceNumber = item.InvoiceNumber?.Trim();
            item.IpAddress = item.IpAddress?.Trim();
            item.MacAddress = item.MacAddress?.Trim();
            item.OperatingSystem = item.OperatingSystem?.Trim();
            item.OfficeVersion = item.OfficeVersion?.Trim();
            item.Antivirus = item.Antivirus?.Trim();

            if (string.IsNullOrWhiteSpace(item.AssetCode))
            {
                item.AssetCode = await GenerateAssetCodeAsync();
            }

            if (await _context.InventoryItems.AnyAsync(i => i.AssetCode == item.AssetCode))
            {
                ModelState.AddModelError(nameof(InventoryItem.AssetCode), "El código de activo ya existe.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(item.Category, item.Status, item.Site, item.Department, item.OperatingSystem, item.OfficeVersion, item.IsActive == false);
                return View(item);
            }

            item.CreatedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);
            if (item == null)
            {
                return NotFound();
            }

            await PopulateOptionsAsync(item.Category, item.Status, item.Site, item.Department, item.OperatingSystem, item.OfficeVersion, item.IsActive == false);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Edit(int id, InventoryItem item)
        {
            if (id != item.Id)
            {
                return NotFound();
            }

            if (item.RowVersion == null)
            {
                ModelState.AddModelError(string.Empty, "No se pudo validar concurrencia del activo.");
                await PopulateOptionsAsync(item.Category, item.Status, item.Site, item.Department, item.OperatingSystem, item.OfficeVersion, item.IsActive == false);
                return View(item);
            }

            var dbItem = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);
            if (dbItem == null)
            {
                return NotFound();
            }

            if (await _context.InventoryItems.AnyAsync(i => i.Id != item.Id && i.AssetCode == item.AssetCode))
            {
                ModelState.AddModelError(nameof(InventoryItem.AssetCode), "El código de activo ya existe.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(item.Category, item.Status, item.Site, item.Department, item.OperatingSystem, item.OfficeVersion, item.IsActive == false);
                return View(item);
            }

            _context.Entry(dbItem).Property(i => i.RowVersion).OriginalValue = item.RowVersion;

            dbItem.AssetCode = (item.AssetCode ?? string.Empty).Trim();
            dbItem.AssetTag = item.AssetTag?.Trim();
            dbItem.SerialNumber = item.SerialNumber?.Trim();
            dbItem.Category = (item.Category ?? string.Empty).Trim();
            dbItem.Subcategory = item.Subcategory?.Trim();
            dbItem.Brand = item.Brand?.Trim();
            dbItem.Model = item.Model?.Trim();
            dbItem.TechnicalSpecifications = item.TechnicalSpecifications?.Trim();
            dbItem.Status = (item.Status ?? string.Empty).Trim();
            dbItem.Condition = (item.Condition ?? string.Empty).Trim();
            dbItem.Site = item.Site?.Trim();
            dbItem.Department = item.Department?.Trim();
            dbItem.AssignedTo = item.AssignedTo?.Trim();
            dbItem.AssignedToEmail = item.AssignedToEmail?.Trim();
            dbItem.CorporatePhoneNumber = item.CorporatePhoneNumber?.Trim();
            dbItem.PurchaseDate = item.PurchaseDate;
            dbItem.WarrantyEndDate = item.WarrantyEndDate;
            dbItem.Supplier = item.Supplier?.Trim();
            dbItem.InvoiceNumber = item.InvoiceNumber?.Trim();
            dbItem.CostCordoba = item.CostCordoba;
            dbItem.CostUsd = item.CostUsd;
            dbItem.IpAddress = item.IpAddress?.Trim();
            dbItem.MacAddress = item.MacAddress?.Trim();
            dbItem.OperatingSystem = item.OperatingSystem?.Trim();
            dbItem.OfficeVersion = item.OfficeVersion?.Trim();
            dbItem.Antivirus = item.Antivirus?.Trim();
            dbItem.LastMaintenanceDate = item.LastMaintenanceDate;
            dbItem.NextMaintenanceDate = item.NextMaintenanceDate;
            dbItem.Notes = item.Notes?.Trim();
            dbItem.IsActive = item.IsActive;
            dbItem.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "El activo fue modificado por otro usuario.");
                item.RowVersion = dbItem.RowVersion;
                await PopulateOptionsAsync(item.Category, item.Status, item.Site, item.Department, item.OperatingSystem, item.OfficeVersion, item.IsActive == false);
                return View(item);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);
            if (item == null)
            {
                return NotFound();
            }

            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ExportExcel(
            string? search,
            string? category,
            string? status,
            string? site,
            bool includeInactive = false)
        {
            var items = await BuildFilteredQuery(search, category, status, site, includeInactive)
                .OrderBy(i => i.Category)
                .ThenBy(i => i.AssetCode)
                .ToListAsync();

            var html = BuildExcelHtml(items);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(html)).ToArray();

            var fileName = $"InventarioTI_{DateTime.Now:yyyyMMdd_HHmm}.xls";
            return File(bytes, "application/vnd.ms-excel", fileName);
        }

        public async Task<IActionResult> ExportPdf(
            string? search,
            string? category,
            string? status,
            string? site,
            bool includeInactive = false)
        {
            var items = await BuildFilteredQuery(search, category, status, site, includeInactive)
                .OrderBy(i => i.Category)
                .ThenBy(i => i.AssetCode)
                .ToListAsync();

            var bytes = InventoryPdfReportService.GenerateInventoryReportPdf(items, _environment.WebRootPath);
            var fileName = $"InventarioTI_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return File(bytes, "application/pdf", fileName);
        }

        private IQueryable<InventoryItem> BuildFilteredQuery(
            string? search,
            string? category,
            string? status,
            string? site,
            bool includeInactive)
        {
            IQueryable<InventoryItem> query = _context.InventoryItems.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(i => i.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(i =>
                    i.AssetCode.Contains(term) ||
                    (i.AssetTag != null && i.AssetTag.Contains(term)) ||
                    (i.SerialNumber != null && i.SerialNumber.Contains(term)) ||
                    (i.Brand != null && i.Brand.Contains(term)) ||
                    (i.Model != null && i.Model.Contains(term)) ||
                    (i.AssignedTo != null && i.AssignedTo.Contains(term)) ||
                    (i.AssignedToEmail != null && i.AssignedToEmail.Contains(term)) ||
                    (i.CorporatePhoneNumber != null && i.CorporatePhoneNumber.Contains(term)) ||
                    (i.Site != null && i.Site.Contains(term)) ||
                    (i.Department != null && i.Department.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(i => i.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(i => i.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(site))
            {
                query = query.Where(i => i.Site == site);
            }

            return query;
        }

        private async Task PopulateOptionsAsync(
            string? selectedCategory,
            string? selectedStatus,
            string? selectedSite,
            string? selectedDepartment,
            string? selectedOperatingSystem,
            string? selectedOfficeVersion,
            bool includeInactive)
        {
            var baseCategories = new List<string>
            {
                "Laptop",
                "Desktop",
                "Servidor",
                "Impresora",
                "Router",
                "Switch",
                "Access Point",
                "UPS",
                "Monitor",
                "Telefono IP",
                "Periferico",
                "Licencia Software",
                "Otro"
            };

            var baseStatuses = new List<string>
            {
                "Disponible",
                "Asignado",
                "En Mantenimiento",
                "Dañado",
                "En Baja"
            };

            var baseSites = new List<string>
            {
                "Oficina ASOMA",
                "Plantel Nagarote",
                "Plantel Las Lajitas",
                "Granada",
                "Jinotepe",
                "Casa Miramar",
                "Casa de Alto Nagarote"
            };

            var baseDepartments = new List<string>
            {
                "IT",
                "Operaciones",
                "Administracion",
                "Compras",
                "Contabilidad"
            };

            var baseOperatingSystems = new List<string>
            {
                "Windows 2000 Professional",
                "Windows XP",
                "Windows Server 2003",
                "Windows Vista",
                "Windows Server 2008",
                "Windows 7",
                "Windows Server 2008 R2",
                "Windows 8",
                "Windows Server 2012",
                "Windows 8.1",
                "Windows Server 2012 R2",
                "Windows 10",
                "Windows Server 2016",
                "Windows Server 2019",
                "Windows 11",
                "Windows Server 2022",
                "macOS 10.15 Catalina",
                "macOS 11 Big Sur",
                "macOS 12 Monterey",
                "macOS 13 Ventura",
                "macOS 14 Sonoma",
                "macOS 15 Sequoia",
                "Ubuntu 20.04 LTS",
                "Ubuntu 22.04 LTS",
                "Ubuntu 24.04 LTS"
            };

            var baseOfficeVersions = new List<string>
            {
                "Office 2000",
                "Office XP (2002)",
                "Office 2003",
                "Office 2007",
                "Office 2010",
                "Office 2013",
                "Office 2016",
                "Office 2019",
                "Office 2021",
                "Office LTSC 2024",
                "Microsoft 365 Apps"
            };

            var dbCategories = await _context.InventoryItems
                .AsNoTracking()
                .Where(i => !string.IsNullOrWhiteSpace(i.Category))
                .Select(i => i.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var dbStatuses = await _context.InventoryItems
                .AsNoTracking()
                .Where(i => !string.IsNullOrWhiteSpace(i.Status))
                .Select(i => i.Status)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            var dbSites = await _context.InventoryItems
                .AsNoTracking()
                .Where(i => !string.IsNullOrWhiteSpace(i.Site))
                .Select(i => i.Site!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            var inventoryDepartments = await _context.InventoryItems
                .AsNoTracking()
                .Where(i => !string.IsNullOrWhiteSpace(i.Department))
                .Select(i => i.Department!.Trim())
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var ticketDepartments = await _context.Tickets
                .AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.Department))
                .Select(t => t.Department!.Trim())
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var inventoryOperatingSystems = await _context.InventoryItems
                .AsNoTracking()
                .Where(i => !string.IsNullOrWhiteSpace(i.OperatingSystem))
                .Select(i => i.OperatingSystem!.Trim())
                .Distinct()
                .OrderBy(os => os)
                .ToListAsync();

            var inventoryOfficeVersions = await _context.InventoryItems
                .AsNoTracking()
                .Where(i => !string.IsNullOrWhiteSpace(i.OfficeVersion))
                .Select(i => i.OfficeVersion!.Trim())
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();

            var categories = baseCategories
                .Concat(dbCategories.Where(c => !baseCategories.Contains(c, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(c => new SelectListItem(c, c, c == selectedCategory))
                .ToList();

            var statuses = baseStatuses
                .Concat(dbStatuses.Where(s => !baseStatuses.Contains(s, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(s => new SelectListItem(s, s, s == selectedStatus))
                .ToList();

            var sites = baseSites
                .Concat(dbSites.Where(s => !baseSites.Contains(s, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(s => new SelectListItem(s, s, s == selectedSite))
                .ToList();

            var departments = baseDepartments
                .Concat(ticketDepartments.Where(d => !baseDepartments.Contains(d, StringComparer.OrdinalIgnoreCase)))
                .Concat(inventoryDepartments.Where(d => !baseDepartments.Contains(d, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(d => new SelectListItem(d, d, d == selectedDepartment))
                .ToList();

            var operatingSystems = baseOperatingSystems
                .Concat(inventoryOperatingSystems.Where(os => !baseOperatingSystems.Contains(os, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(os => new SelectListItem(os, os, os == selectedOperatingSystem))
                .ToList();

            var officeVersions = baseOfficeVersions
                .Concat(inventoryOfficeVersions.Where(v => !baseOfficeVersions.Contains(v, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(v => new SelectListItem(v, v, v == selectedOfficeVersion))
                .ToList();

            ViewBag.CategoryOptions = categories;
            ViewBag.StatusOptions = statuses;
            ViewBag.SiteOptions = sites;
            ViewBag.DepartmentOptions = departments;
            ViewBag.OperatingSystemOptions = operatingSystems;
            ViewBag.OfficeVersionOptions = officeVersions;
            ViewBag.IncludeInactive = includeInactive;
        }

        private async Task<string> GenerateAssetCodeAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"INV-{year}-";

            var lastCode = await _context.InventoryItems
                .AsNoTracking()
                .Where(i => i.AssetCode.StartsWith(prefix))
                .OrderByDescending(i => i.AssetCode)
                .Select(i => i.AssetCode)
                .FirstOrDefaultAsync();

            var nextNumber = 1;
            if (!string.IsNullOrWhiteSpace(lastCode))
            {
                var parts = lastCode.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out var number))
                {
                    nextNumber = number + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        private static string BuildExcelHtml(IEnumerable<InventoryItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><meta charset='utf-8'></head><body>");
            sb.AppendLine($"<h2>Inventario TI - {DateTime.Now:dd/MM/yyyy HH:mm}</h2>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr style='font-weight:bold;background:#f2f2f2;'>");
            sb.AppendLine("<td>Codigo</td><td>Activo</td><td>Categoria</td><td>Marca</td><td>Modelo</td><td>Serie</td><td>Estado</td><td>Condicion</td><td>Sitio</td><td>Departamento</td><td>Asignado a</td><td>Email</td><td>Tel. corporativo</td><td>Fecha compra</td><td>Garantia</td><td>Costo C$</td><td>Costo $</td><td>IP</td><td>MAC</td><td>SO</td><td>Office</td><td>Antivirus</td><td>Ultimo mantenimiento</td><td>Proximo mantenimiento</td><td>Notas</td>");
            sb.AppendLine("</tr>");

            foreach (var item in items)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{E(item.AssetCode)}</td>");
                sb.AppendLine($"<td>{(item.IsActive ? "Si" : "No")}</td>");
                sb.AppendLine($"<td>{E(item.Category)}</td>");
                sb.AppendLine($"<td>{E(item.Brand)}</td>");
                sb.AppendLine($"<td>{E(item.Model)}</td>");
                sb.AppendLine($"<td>{E(item.SerialNumber)}</td>");
                sb.AppendLine($"<td>{E(item.Status)}</td>");
                sb.AppendLine($"<td>{E(item.Condition)}</td>");
                sb.AppendLine($"<td>{E(item.Site)}</td>");
                sb.AppendLine($"<td>{E(item.Department)}</td>");
                sb.AppendLine($"<td>{E(item.AssignedTo)}</td>");
                sb.AppendLine($"<td>{E(item.AssignedToEmail)}</td>");
                sb.AppendLine($"<td>{E(item.CorporatePhoneNumber)}</td>");
                sb.AppendLine($"<td>{D(item.PurchaseDate)}</td>");
                sb.AppendLine($"<td>{D(item.WarrantyEndDate)}</td>");
                sb.AppendLine($"<td>{N(item.CostCordoba)}</td>");
                sb.AppendLine($"<td>{N(item.CostUsd)}</td>");
                sb.AppendLine($"<td>{E(item.IpAddress)}</td>");
                sb.AppendLine($"<td>{E(item.MacAddress)}</td>");
                sb.AppendLine($"<td>{E(item.OperatingSystem)}</td>");
                sb.AppendLine($"<td>{E(item.OfficeVersion)}</td>");
                sb.AppendLine($"<td>{E(item.Antivirus)}</td>");
                sb.AppendLine($"<td>{D(item.LastMaintenanceDate)}</td>");
                sb.AppendLine($"<td>{D(item.NextMaintenanceDate)}</td>");
                sb.AppendLine($"<td>{E(item.Notes)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table></body></html>");
            return sb.ToString();

            static string E(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
            static string D(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
            static string N(decimal? value) => value?.ToString("N2", CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}






