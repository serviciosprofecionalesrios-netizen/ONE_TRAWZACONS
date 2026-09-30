using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITServiceDeskApp.Services;
using System.Globalization;

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

        public async Task<IActionResult> Fuente(string section = "inventario", string? q = null, DateTime? from = null, DateTime? to = null, int page = 1, CancellationToken cancellationToken = default)
        {
            var sheet = PublishedInventoryService.Sheets.FirstOrDefault(x => x.Key == section);
            if (sheet == null) return NotFound();
            var source = await _published.GetAsync(sheet, cancellationToken);
            var query = (q ?? "").Trim();
            if (query.Length > 200) return BadRequest("La búsqueda debe tener como máximo 200 caracteres.");
            var dateIndex = source.Table == null ? -1 : Array.FindIndex(source.Table.Headers, h => h.Contains("FECHA", StringComparison.OrdinalIgnoreCase) || h.Contains("Marca de Tiempo", StringComparison.OrdinalIgnoreCase));
            var rows = (source.Table?.Rows ?? []).Where(r => query.Length == 0 || r.Cells.Any(c => c.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .Where(r => dateIndex < 0 || (!from.HasValue && !to.HasValue) || TryDate(r.Cells[dateIndex], out var d) && (!from.HasValue || d.Date >= from.Value.Date) && (!to.HasValue || d.Date <= to.Value.Date))
                .OrderByDescending(r => dateIndex >= 0 && TryDate(r.Cells[dateIndex], out var parsed) ? parsed : DateTime.MinValue).ToArray();
            page = Math.Clamp(page, 1, Math.Max(1, (rows.Length + 49) / 50));
            return View(new PublishedInventoryViewModel
            {
                Sheet = sheet, Source = source, Query = query, From = from, To = to, Page = page,
                TotalRows = rows.Length, Rows = rows.Skip((page - 1) * 50).Take(50).ToArray()
            });
        }

        private static bool TryDate(string value, out DateTime date) =>
            DateTime.TryParse(value, new System.Globalization.CultureInfo("es-NI"), System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date) ||
            DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date);

        private static decimal ParseAmount(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0m;
            var clean = value.Replace("C$", "", StringComparison.OrdinalIgnoreCase).Replace("US$", "", StringComparison.OrdinalIgnoreCase).Replace("$", "").Trim();
            return decimal.TryParse(clean, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, new CultureInfo("es-NI"), out var result) ||
                   decimal.TryParse(clean, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out result)
                ? result : 0m;
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

        public async Task<IActionResult> ComprasDashboard(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
        {
            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            {
                (from, to) = (to, from);
            }

            var sheet = PublishedInventoryService.Sheets.Single(x => x.Key == "compras");
            var source = await _published.GetAsync(sheet, cancellationToken);
            var table = source.Table;
            if (table == null)
            {
                return View(new PurchaseDashboardViewModel { From = from, To = to, Warning = source.Warning });
            }

            int Column(string name) => Array.FindIndex(table.Headers, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            string Value(InventorySourceRow row, int index) => index >= 0 && index < row.Cells.Length ? row.Cells[index].Trim() : string.Empty;
            var dateColumn = Array.FindIndex(table.Headers, h => h.Contains("FECHA", StringComparison.OrdinalIgnoreCase));
            var orderColumn = Column("ORDEN DE COMPRA");
            var supplierColumn = Column("PROVEEDOR");
            var statusColumn = Column("ESTADO OC");
            var cordobasColumn = Column("TOTAL C$");
            var usdColumn = Column("TOTAL $");
            var descriptionColumn = Column("DESCRIPCION");
            var purchases = table.Rows.Select(row => new PurchaseDashboardOrder(
                    Value(row, orderColumn), Value(row, supplierColumn), Value(row, statusColumn),
                    dateColumn >= 0 && TryDate(Value(row, dateColumn), out var date) ? date.Date : null,
                    ParseAmount(Value(row, cordobasColumn)), ParseAmount(Value(row, usdColumn)), Value(row, descriptionColumn)))
                .Where(x => (!from.HasValue || x.Date.HasValue && x.Date.Value >= from.Value.Date) &&
                            (!to.HasValue || x.Date.HasValue && x.Date.Value <= to.Value.Date))
                .ToList();

            return View(new PurchaseDashboardViewModel
            {
                From = from, To = to,
                LatestPurchaseDate = purchases.Where(x => x.Date.HasValue).Select(x => x.Date).Max(),
                PurchaseLines = purchases.Count,
                PurchaseOrders = purchases.Select((x, index) => string.IsNullOrWhiteSpace(x.OrderNumber) ? $"fila-{index}" : x.OrderNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                TotalCordobas = purchases.Sum(x => x.Cordobas), TotalUsd = purchases.Sum(x => x.Usd),
                ByStatus = purchases.GroupBy(x => string.IsNullOrWhiteSpace(x.Status) ? "Sin estado" : x.Status)
                    .Select(g => new PurchaseDashboardStatus(g.Key, g.Select(x => x.OrderNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(), g.Sum(x => x.Cordobas), g.Sum(x => x.Usd)))
                    .OrderByDescending(x => x.Cordobas + x.Usd).Take(6).ToList(),
                TopSuppliers = purchases.GroupBy(x => string.IsNullOrWhiteSpace(x.Supplier) ? "Sin proveedor" : x.Supplier)
                    .Select(g => new PurchaseDashboardSupplier(g.Key, g.Select(x => x.OrderNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(), g.Sum(x => x.Cordobas), g.Sum(x => x.Usd)))
                    .OrderByDescending(x => x.Cordobas + x.Usd).Take(8).ToList(),
                RecentOrders = purchases.OrderByDescending(x => x.Date ?? DateTime.MinValue).Take(15).ToList(), Warning = source.Warning
            });
        }
    }
}
