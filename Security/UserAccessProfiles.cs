namespace ITServiceDeskApp.Security;

public static class UserAccessProfiles
{
    public const string ClaimType = "AccessProfile";
    public const string BusinessRoleClaimType = "BusinessRole";
    public const string MaintenanceEditor = "MaintenanceEditor";
    public const string PurchasingEditor = "PurchasingEditor";
    public const string SupervisorOperations = "SupervisorOperations";
    public const string Administrator = "Administrator";
    public const string InventoryMaintenance = "InventoryMaintenance";
    public const string OperationsHsReadOnly = "OperationsHsReadOnly";
    public const string OperationsEditor = "OperationsEditor";
    public const string OperationsHsReporter = "OperationsHsReporter";
    public const string OperationsHsEditor = "OperationsHsEditor";

    private static readonly HashSet<string> Administrators = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin@trawzacons.com", "jzamora@trawzacons.com", "trawza12@hotmail.com", "ezamora@trawzacons.com"
    };

    private static readonly HashSet<string> InventoryMaintenanceUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "ecanales@trawzacons.com", "aparedes@trawzacons.com", "almacenact@trawzacons.com", "almacenpsb@trawzacons.com"
    };

    private static readonly HashSet<string> OperationsHsReadOnlyUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "roldanzamora@trawzacons.com"
    };

    private static readonly HashSet<string> OperationsHsEditorUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "leslieantonioespinoza@trawzacons.com", "Espinozaleslieantonioespinoza@trawzacons.com"
    };

    private static readonly HashSet<string> OperationsEditorUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "monitoreo2@trawzacons.com", "monitoreo@trawzacons.com", "monitoreo2@trawzacions.com"
    };

    private static readonly HashSet<string> OperationsHsReporterUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "coordinadorlaliberta@trawzacons.com", "yerlinrocha@trawzacons.com",
        "cordinadorminalimon@trawzacons.com", "coordinadorminalimon@trawzacons.com"
    };

    public static string? Resolve(string email)
    {
        if (Administrators.Contains(email)) return Administrator;
        if (InventoryMaintenanceUsers.Contains(email)) return InventoryMaintenance;
        if (OperationsHsEditorUsers.Contains(email)) return OperationsHsEditor;
        if (OperationsHsReadOnlyUsers.Contains(email)) return OperationsHsReadOnly;
        if (OperationsEditorUsers.Contains(email)) return OperationsEditor;
        if (OperationsHsReporterUsers.Contains(email)) return OperationsHsReporter;
        return null;
    }

    public static bool IsAdministrator(string? email) =>
        !string.IsNullOrWhiteSpace(email) && Administrators.Contains(email.Trim());

    public static string? Resolve(string email, string? role)
    {
        if (IsAdministrator(email)) return Administrator;
        if (role == nameof(ITServiceDeskApp.Models.UserRole.GerenteTaller)) return MaintenanceEditor;
        if (role == nameof(ITServiceDeskApp.Models.UserRole.GerenteCompras)) return PurchasingEditor;
        var configured = Resolve(email.Trim());
        if (configured != null) return configured;
        return role switch
        {
            nameof(ITServiceDeskApp.Models.UserRole.GestorHS) or
            nameof(ITServiceDeskApp.Models.UserRole.GerenteHS) or
            nameof(ITServiceDeskApp.Models.UserRole.SupervisorHS) => OperationsHsEditor,
            nameof(ITServiceDeskApp.Models.UserRole.SupervisorTaller) or
            nameof(ITServiceDeskApp.Models.UserRole.SupervisorLogistica) or
            nameof(ITServiceDeskApp.Models.UserRole.SupervisorGeneralPSB) or
            nameof(ITServiceDeskApp.Models.UserRole.SupervisorEncargadoML) or
            nameof(ITServiceDeskApp.Models.UserRole.SupervisorML) or
            nameof(ITServiceDeskApp.Models.UserRole.SupervisorEncargadoMLL) or
            nameof(ITServiceDeskApp.Models.UserRole.SupervisorMLL) => SupervisorOperations,
            _ => null
        };
    }

    public static string? Resolve(System.Security.Claims.ClaimsPrincipal user) =>
        Resolve(user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "",
            user.FindFirst(BusinessRoleClaimType)?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value)
        ?? user.FindFirst(ClaimType)?.Value;

    public static bool CanCompleteHsDocuments(System.Security.Claims.ClaimsPrincipal user)
    {
        var role = user.FindFirst(BusinessRoleClaimType)?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        return user.Identity?.IsAuthenticated == true &&
            (Resolve(user) == OperationsHsEditor || role is "GestorHS" or "GerenteHS" or "SupervisorHS");
    }

    public static bool CanMaintainDrivers(System.Security.Claims.ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true &&
        (IsAdministrator(user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value) ||
         (Resolve(user) == null && user.IsInRole("Administrator")) ||
         new[] { "roldanzamora@trawzacons.com", "monitoreo2@trawzacons.com", "monitoreo@trawzacons.com", "monitoreo2@trawzacions.com" }
             .Contains(user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value?.Trim(), StringComparer.OrdinalIgnoreCase));

    public static bool CanMaintainMaster(System.Security.Claims.ClaimsPrincipal user, string kind) =>
        kind == "conductores" ? CanMaintainDrivers(user) : CanMaintainOperationalMasters(user);

    public static bool CanMaintainOperationalMasters(System.Security.Claims.ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true &&
        (IsAdministrator(user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value) ||
         Resolve(user) == OperationsEditor ||
         (Resolve(user) == null && (user.IsInRole("Administrator") || user.IsInRole("CoordinadorIT") || user.IsInRole("Technician"))));

    public static (string Controller, string Action) Home(string? profile) => profile switch
    {
        InventoryMaintenance or PurchasingEditor => ("InventoryHub", "Index"),
        MaintenanceEditor => ("MaintenanceRepairs", "Index"),
        OperationsHsReadOnly or OperationsEditor or OperationsHsReporter or OperationsHsEditor => ("Operaciones", "Dashboard"),
        _ => ("Operaciones", "Dashboard")
    };
}
