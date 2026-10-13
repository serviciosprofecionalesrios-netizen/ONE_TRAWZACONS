using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ITServiceDeskApp.Security;

public sealed class AccessProfileAuthorizationFilter : IAuthorizationFilter
{
    private static readonly HashSet<string> InventoryAndMaintenanceControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Inventory", "InventoryHub", "InventoryMaster", "MaintenanceAppointments",
        "MaintenanceInventory", "MaintenanceRepairs", "MaintenanceReports", "MaintenanceTechnicians", "Tickets"
    };

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var profile = user.FindFirst(UserAccessProfiles.ClaimType)?.Value;
        var email = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        // Use the current configured permissions even when a session has an older profile claim.
        if (!string.IsNullOrWhiteSpace(email)) profile = UserAccessProfiles.Resolve(email) ?? profile;
        var isConfiguredAdministrator = UserAccessProfiles.IsAdministrator(email);
        var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        if (controller.Equals("Account", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var action = context.ActionDescriptor.RouteValues["action"] ?? string.Empty;
        var isDeletion = HttpMethods.IsDelete(context.HttpContext.Request.Method) ||
            action.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("remove", StringComparison.OrdinalIgnoreCase);
        if (isDeletion && !isConfiguredAdministrator)
        {
            context.Result = new ForbidResult();
            return;
        }

        if (controller.Equals("Audit", StringComparison.OrdinalIgnoreCase) && !isConfiguredAdministrator)
        {
            context.Result = new ForbidResult();
            return;
        }

        if (string.IsNullOrWhiteSpace(profile) || isConfiguredAdministrator)
        {
            return;
        }

        var isReadRequest = HttpMethods.IsGet(context.HttpContext.Request.Method) ||
                            HttpMethods.IsHead(context.HttpContext.Request.Method);
        var isToneladasUpload = HttpMethods.IsPost(context.HttpContext.Request.Method) &&
            controller.Equals("Operaciones", StringComparison.OrdinalIgnoreCase) &&
            action.Equals("SeguimientoToneladas", StringComparison.OrdinalIgnoreCase);

        var permitted = profile switch
        {
            UserAccessProfiles.InventoryMaintenance =>
                InventoryAndMaintenanceControllers.Contains(controller) ||
                (controller.Equals("Operaciones", StringComparison.OrdinalIgnoreCase) &&
                 action.Equals("Dashboard", StringComparison.OrdinalIgnoreCase)),
            UserAccessProfiles.OperationsEditor => controller.Equals("Operaciones", StringComparison.OrdinalIgnoreCase),
            UserAccessProfiles.OperationsHsEditor =>
                controller.Equals("Hs", StringComparison.OrdinalIgnoreCase) ||
                controller.Equals("HsDriverScore", StringComparison.OrdinalIgnoreCase) ||
                (controller.Equals("Operaciones", StringComparison.OrdinalIgnoreCase) &&
                 (isReadRequest || isToneladasUpload ||
                  (HttpMethods.IsPost(context.HttpContext.Request.Method) &&
                   (action.Equals("NuevaInfraccion", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("SaveDriverScoreCase", StringComparison.OrdinalIgnoreCase))))),
            UserAccessProfiles.OperationsHsReadOnly or UserAccessProfiles.OperationsHsReporter =>
                isToneladasUpload || (isReadRequest && (controller.Equals("Operaciones", StringComparison.OrdinalIgnoreCase) ||
                                  controller.Equals("Hs", StringComparison.OrdinalIgnoreCase) || controller.Equals("HsDriverScore", StringComparison.OrdinalIgnoreCase))),
            _ => false
        };

        if (!permitted)
        {
            context.Result = new ForbidResult();
        }
    }
}
