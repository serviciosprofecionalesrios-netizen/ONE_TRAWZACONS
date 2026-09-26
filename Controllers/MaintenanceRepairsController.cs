using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Inventory;
using ITServiceDeskApp.ViewModels.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers;

[Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
public class MaintenanceRepairsController(
    PublishedInventoryService sourceService,
    ApplicationDbContext context,
    IWebHostEnvironment environment) : Controller
{
    public async Task<IActionResult> Index(string? q = null, string? status = null, DateTime? from = null, DateTime? to = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = (q ?? "").Trim();
        var selectedStatus = (status ?? "").Trim();
        if (query.Length > 200 || selectedStatus.Length > 100) return BadRequest("El filtro es demasiado largo.");

        var systemRepairsQuery = context.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Department == "Mantenimiento");

        if (query.Length > 0)
        {
            systemRepairsQuery = systemRepairsQuery.Where(ticket =>
                (ticket.TicketNumber != null && ticket.TicketNumber.Contains(query)) ||
                (ticket.UnitCode != null && ticket.UnitCode.Contains(query)) ||
                ticket.Description.Contains(query) ||
                (ticket.AssignedTechnician != null && ticket.AssignedTechnician.Contains(query)));
        }

        var systemRepairs = await systemRepairsQuery
            .OrderByDescending(ticket => ticket.CreatedDate)
            .Take(100)
            .ToListAsync(cancellationToken);

        ViewBag.SystemRepairs = systemRepairs;
        var source = await sourceService.GetAsync(PublishedInventoryService.Repairs, cancellationToken);
        var table = source.Table;
        var externalOverrides = await context.MaintenanceExternalRepairs
            .AsNoTracking()
            .ToDictionaryAsync(x => x.SourceRepairNumber, StringComparer.OrdinalIgnoreCase, cancellationToken);
        ViewBag.ExternalRepairOverrides = externalOverrides;
        var counts = table?.Rows.GroupBy(r => PublishedRepairsViewModel.State(table, r), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) ?? new Dictionary<string, int>();
        var dateIndex = table == null ? -1 : Array.FindIndex(table.Headers, h => h.Equals("Fecha de entrada", StringComparison.OrdinalIgnoreCase));
        var rows = (table?.Rows ?? []).Where(r =>
            (query.Length == 0 || r.Cells.Any(c => c.Contains(query, StringComparison.OrdinalIgnoreCase))) &&
            (selectedStatus.Length == 0 || PublishedRepairsViewModel.State(table!, r).Equals(selectedStatus, StringComparison.OrdinalIgnoreCase)) &&
            (dateIndex < 0 || (!from.HasValue && !to.HasValue) || TryDate(r.Cells[dateIndex], out var d) && (!from.HasValue || d.Date >= from.Value.Date) && (!to.HasValue || d.Date <= to.Value.Date)))
            .OrderByDescending(r => dateIndex >= 0 && TryDate(r.Cells[dateIndex], out var parsed) ? parsed : DateTime.MinValue).ToArray();
        page = Math.Clamp(page, 1, Math.Max(1, (rows.Length + 49) / 50));
        return View(new PublishedRepairsViewModel
        {
            Sheet = PublishedInventoryService.Repairs, Source = source, Query = query,
            Status = selectedStatus, StatusCounts = counts, From = from, To = to, TotalRows = rows.Length, Page = page,
            Rows = rows.Skip((page - 1) * 50).Take(50).ToArray()
        });
    }

    [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
    public async Task<IActionResult> EditExternal(string repairNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repairNumber) || repairNumber.Length > 120) return BadRequest();
        var source = await sourceService.GetAsync(PublishedInventoryService.Repairs, cancellationToken);
        var row = source.Table?.Rows.FirstOrDefault(x => Value(source.Table, x, "N° Gestion").Equals(repairNumber.Trim(), StringComparison.OrdinalIgnoreCase));
        if (source.Table == null || row == null) return NotFound();

        var existing = await context.MaintenanceExternalRepairs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SourceRepairNumber == repairNumber.Trim(), cancellationToken);
        return View(new ExternalRepairEditViewModel
        {
            SourceRepairNumber = repairNumber.Trim(),
            EquipmentCode = Value(source.Table, row, "Codigo de Equipo"),
            ProblemDescription = Value(source.Table, row, "Descripción del problema"),
            Status = existing?.Status ?? PublishedRepairsViewModel.State(source.Table, row),
            AssignedMechanic = existing?.AssignedMechanic ?? Value(source.Table, row, "Mecanico Asignado"),
            WorkPerformed = existing?.WorkPerformed ?? Value(source.Table, row, "Descripción del trabajo realizado"),
            DeliveryDocumentPath = existing?.DeliveryDocumentPath
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician")]
    public async Task<IActionResult> EditExternal(ExternalRepairEditViewModel model, IFormFile? deliveryDocumentFile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.SourceRepairNumber) || model.SourceRepairNumber.Length > 120) return BadRequest();
        var allowedStatuses = new[] { "Pendiente", "En reparacion", "Terminado", "Entregado" };
        if (!allowedStatuses.Contains(model.Status, StringComparer.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(model.Status), "Seleccione un estado válido.");

        if (model.Status.Equals("Entregado", StringComparison.OrdinalIgnoreCase) &&
            (deliveryDocumentFile == null || deliveryDocumentFile.Length == 0) && string.IsNullOrWhiteSpace(model.DeliveryDocumentPath))
            ModelState.AddModelError(nameof(model.DeliveryDocumentPath), "Adjunte el PDF de entrega antes de marcar como Entregado.");

        if (deliveryDocumentFile is { Length: > 0 } &&
            (!Path.GetExtension(deliveryDocumentFile.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) || deliveryDocumentFile.Length > 10 * 1024 * 1024))
            ModelState.AddModelError(nameof(model.DeliveryDocumentPath), "El documento debe ser un PDF de máximo 10 MB.");

        if (!ModelState.IsValid) return View(model);

        var item = await context.MaintenanceExternalRepairs
            .FirstOrDefaultAsync(x => x.SourceRepairNumber == model.SourceRepairNumber, cancellationToken)
            ?? new MaintenanceExternalRepair { SourceRepairNumber = model.SourceRepairNumber };
        if (item.Id == 0) context.MaintenanceExternalRepairs.Add(item);

        item.Status = model.Status.Trim();
        item.AssignedMechanic = string.IsNullOrWhiteSpace(model.AssignedMechanic) ? null : model.AssignedMechanic.Trim();
        item.WorkPerformed = string.IsNullOrWhiteSpace(model.WorkPerformed) ? null : model.WorkPerformed.Trim();
        item.UpdatedBy = User.Identity?.Name?.Trim();
        item.UpdatedAtUtc = DateTime.UtcNow;
        if (deliveryDocumentFile is { Length: > 0 }) item.DeliveryDocumentPath = await SaveDeliveryPdfAsync(deliveryDocumentFile);

        await context.SaveChangesAsync(cancellationToken);
        TempData["ExternalRepairMessage"] = $"Reparación {item.SourceRepairNumber} actualizada.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> SaveDeliveryPdfAsync(IFormFile file)
    {
        var directory = Path.Combine(environment.WebRootPath, "uploads", "external-repairs");
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}.pdf";
        await using var stream = new FileStream(Path.Combine(directory, fileName), FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/external-repairs/{fileName}";
    }

    private static string Value(InventorySourceTable table, InventorySourceRow row, string header)
    {
        var index = Array.FindIndex(table.Headers, item => item.Equals(header, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? row.Cells[index] : string.Empty;
    }

    private static bool TryDate(string value, out DateTime date) => DateTime.TryParse(value, new System.Globalization.CultureInfo("es-NI"), System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date);
}
