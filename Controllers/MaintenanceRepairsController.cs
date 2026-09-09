using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITServiceDeskApp.Controllers;

[Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
public class MaintenanceRepairsController(PublishedInventoryService sourceService) : Controller
{
    public async Task<IActionResult> Index(string? q = null, string? status = null, DateTime? from = null, DateTime? to = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = (q ?? "").Trim();
        var selectedStatus = (status ?? "").Trim();
        if (query.Length > 200 || selectedStatus.Length > 100) return BadRequest("El filtro es demasiado largo.");
        var source = await sourceService.GetAsync(PublishedInventoryService.Repairs, cancellationToken);
        var table = source.Table;
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

    private static bool TryDate(string value, out DateTime date) => DateTime.TryParse(value, new System.Globalization.CultureInfo("es-NI"), System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date);
}
