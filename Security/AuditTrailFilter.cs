using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ITServiceDeskApp.Security;

public sealed class AuditTrailFilter(ApplicationDbContext database) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var action = context.ActionDescriptor.RouteValues["action"] ?? string.Empty;
        var method = context.HttpContext.Request.Method;
        var isGeneration = action.StartsWith("Export", StringComparison.OrdinalIgnoreCase) ||
            action.StartsWith("Generate", StringComparison.OrdinalIgnoreCase);
        var isChange = !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method);

        var executed = await next();
        if (!executed.Canceled && executed.Exception is null &&
            !controller.Equals("Account", StringComparison.OrdinalIgnoreCase) &&
            !controller.Equals("Audit", StringComparison.OrdinalIgnoreCase) &&
            (isChange || isGeneration))
        {
            var actionLabel = action.Contains("delete", StringComparison.OrdinalIgnoreCase) || action.Contains("remove", StringComparison.OrdinalIgnoreCase)
                ? "Eliminación" : isGeneration ? "Generación" : "Actualización";
            var id = context.RouteData.Values.TryGetValue("id", out var routeId) && int.TryParse(routeId?.ToString(), out var parsedId)
                ? parsedId : null;
            database.FinanceAuditLogs.Add(new FinanceAuditLog
            {
                EntityName = controller,
                EntityId = id,
                Action = actionLabel,
                PerformedBy = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Sistema",
                PerformedAtUtc = DateTime.UtcNow,
                Details = $"Acción: {action}; método: {method}."
            });
            await database.SaveChangesAsync(context.HttpContext.RequestAborted);
        }
    }
}
