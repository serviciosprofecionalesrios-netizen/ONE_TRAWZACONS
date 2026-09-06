using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class InventoryHubController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventoryHubController(ApplicationDbContext context)
        {
            _context = context;
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

            var model = new InventoryHubViewModel
            {
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
