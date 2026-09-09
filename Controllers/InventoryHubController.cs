using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITServiceDeskApp.Services;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class InventoryHubController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PublishedInventoryService _published;

        public InventoryHubController(ApplicationDbContext context, PublishedInventoryService published)
        {
            _context = context;
            _published = published;
        }

        public async Task<IActionResult> Fuente(string section = "inventario", string? q = null, int page = 1, CancellationToken cancellationToken = default)
        {
            var sheet = PublishedInventoryService.Sheets.FirstOrDefault(x => x.Key == section);
            if (sheet == null) return NotFound();
            var source = await _published.GetAsync(sheet, cancellationToken);
            var query = (q ?? "").Trim();
            if (query.Length > 200) return BadRequest("La búsqueda debe tener como máximo 200 caracteres.");
            var rows = (source.Table?.Rows ?? []).Where(r => query.Length == 0 ||
                r.Cells.Any(c => c.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
            page = Math.Clamp(page, 1, Math.Max(1, (rows.Length + 49) / 50));
            return View(new PublishedInventoryViewModel
            {
                Sheet = sheet, Source = source, Query = query, Page = page,
                TotalRows = rows.Length, Rows = rows.Skip((page - 1) * 50).Take(50).ToArray()
            });
        }

        public async Task<IActionResult> Index()
        {
            var itAssetsTotal = await _context.InventoryItems
                .AsNoTracking()
                .CountAsync();

            var itAssetsActive = await _context.InventoryItems
                .AsNoTracking()
                .CountAsync(x => x.IsActive);

            var sparePartsTotal = await _context.MaintenanceInventoryParts
                .AsNoTracking()
                .CountAsync(x => x.IsActive);

            var sparePartsLowStock = await _context.MaintenanceInventoryParts
                .AsNoTracking()
                .CountAsync(x => x.IsActive && x.QuantityOnHand > 0 && x.QuantityOnHand <= x.MinimumStock);

            var sparePartsOutOfStock = await _context.MaintenanceInventoryParts
                .AsNoTracking()
                .CountAsync(x => x.IsActive && x.QuantityOnHand <= 0);

            var purchaseRequestsOpen = await _context.Tickets
                .AsNoTracking()
                .CountAsync(x =>
                    x.Department == "Inventario" &&
                    x.Status != TicketStatus.Closed);

            var published = await Task.WhenAll(PublishedInventoryService.Sheets.Select(async sheet =>
                new KeyValuePair<string, InventorySourceResult>(sheet.Key, await _published.GetAsync(sheet, HttpContext.RequestAborted))));

            var model = new InventoryHubViewModel
            {
                PublishedSources = published.ToDictionary(x => x.Key, x => x.Value),
                ItAssetsTotal = itAssetsTotal,
                ItAssetsActive = itAssetsActive,
                SparePartsTotal = sparePartsTotal,
                SparePartsLowStock = sparePartsLowStock,
                SparePartsOutOfStock = sparePartsOutOfStock,
                PurchaseRequestsOpen = purchaseRequestsOpen
            };

            return View(model);
        }
    }
}
