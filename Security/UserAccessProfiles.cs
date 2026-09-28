namespace ITServiceDeskApp.Security;

public static class UserAccessProfiles
{
    public const string ClaimType = "AccessProfile";
    public const string Administrator = "Administrator";
    public const string InventoryMaintenance = "InventoryMaintenance";
    public const string OperationsHsReadOnly = "OperationsHsReadOnly";
    public const string OperationsEditor = "OperationsEditor";
    public const string OperationsHsReporter = "OperationsHsReporter";

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
        "roldanzamora@trawzacons.com", "leslieantonioespinoza@trawzacons.com"
    };

    private static readonly HashSet<string> OperationsEditorUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "monitoreo2@trawzacons.com", "monitoreo@trawzacons.com"
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
        if (OperationsHsReadOnlyUsers.Contains(email)) return OperationsHsReadOnly;
        if (OperationsEditorUsers.Contains(email)) return OperationsEditor;
        if (OperationsHsReporterUsers.Contains(email)) return OperationsHsReporter;
        return null;
    }

    public static bool IsAdministrator(string? email) =>
        !string.IsNullOrWhiteSpace(email) && Administrators.Contains(email.Trim());

    public static (string Controller, string Action) Home(string? profile) => profile switch
    {
        InventoryMaintenance => ("InventoryHub", "Index"),
        OperationsHsReadOnly or OperationsEditor or OperationsHsReporter => ("Operaciones", "Dashboard"),
        _ => ("Operaciones", "Dashboard")
    };
}
