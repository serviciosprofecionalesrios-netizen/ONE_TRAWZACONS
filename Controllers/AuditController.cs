using ITServiceDeskApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers;

[Authorize(Roles = "Administrator")]
public class AuditController(ApplicationDbContext context) : Controller
{
    public async Task<IActionResult> Index(string? module, int page = 1)
    {
        var query = context.FinanceAuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(module)) query = query.Where(x => x.EntityName == module.Trim());
        const int pageSize = 100;
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(x => x.PerformedAtUtc).Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.Module = module;
        ViewBag.Page = Math.Max(1, page);
        ViewBag.HasNext = total > page * pageSize;
        return View(rows);
    }
}
