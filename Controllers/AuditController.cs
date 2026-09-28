using ITServiceDeskApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers;

[Authorize]
public class AuditController(ApplicationDbContext context) : Controller
{
    public async Task<IActionResult> Index(string? module, int page = 1)
    {
        var query = context.FinanceAuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(module))
        {
            var scope = module.Trim();
            query = scope switch
            {
                "Mantenimiento" => query.Where(x => x.EntityName.StartsWith("Maintenance") || x.EntityName == "Tickets"),
                "Finanzas" => query.Where(x => x.EntityName.StartsWith("Finance")),
                "Inventario" => query.Where(x => x.EntityName.StartsWith("Inventory")),
                "Usuarios" => query.Where(x => x.EntityName == "Users" || x.EntityName == "Credentials"),
                _ => query.Where(x => x.EntityName == scope)
            };
        }
        const int pageSize = 100;
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(x => x.PerformedAtUtc).Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.Module = module;
        ViewBag.Page = Math.Max(1, page);
        ViewBag.HasNext = total > page * pageSize;
        return View(rows);
    }
}
