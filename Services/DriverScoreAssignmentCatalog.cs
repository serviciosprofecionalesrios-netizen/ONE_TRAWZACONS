namespace ITServiceDeskApp.Services;

public static class DriverScoreAssignmentCatalog
{
    public static readonly string[] Sites = ["Mina El Limón", "Mina La Libertad", "PTC", "PSB"];
    public sealed record Contact(string Name, string Email, string[] Sites);
    public static readonly Contact[] Contacts =
    [
        new("William Alberto Gonzalez", "coordinadorminalimon@trawzacons.com", ["Mina El Limón"]),
        new("Elix Arguello", "coordinadorminalimon@trawzacons.com", ["Mina El Limón"]),
        new("Yerlin Rocha", "yerlinrocha@trawzacons.com", ["Mina La Libertad"]),
        new("Dania Esmirna Espinoza", "coordinadorlaliberta@trawzacons.com", ["Mina La Libertad"]),
        new("Leslie", "Espinozaleslieantonioespinoza@trawzacons.com", Sites)
    ];
    public static bool IsValid(string? site, string? responsible) => Sites.Contains(site) &&
        (string.IsNullOrWhiteSpace(responsible) || Contacts.Any(x => x.Name == responsible && x.Sites.Contains(site)));
    public static bool CanComplete(string? site, string? responsible, string? email) =>
        IsValid(site, responsible) && Contacts.Any(x => x.Name == responsible &&
            (x.Email.Equals(email?.Trim(), StringComparison.OrdinalIgnoreCase) ||
             (x.Name == "Leslie" && "leslieantonioespinoza@trawzacons.com".Equals(email?.Trim(), StringComparison.OrdinalIgnoreCase))));
}
