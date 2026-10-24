using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.Services;

/// <summary>Resolves documentary records to one master; ambiguous identities stay unlinked.</summary>
public static class OperationalMasterIdentity
{
    public static OperationalMasterRecord? Resolve(IEnumerable<OperationalMasterRecord> masters, string kind,
        int? masterId, string name, string? plate = null, string? sourceKey = null)
    {
        var candidates = masters.Where(x => x.Kind == kind).ToList();
        if (masterId.HasValue) return candidates.SingleOrDefault(x => x.Id == masterId.Value);
        var key = OperationalMasterCatalog.Key(kind, name);
        var plateKey = PlateKey(plate);
        var matches = candidates.Where(x =>
            (!string.IsNullOrWhiteSpace(name) && (OperationalMasterCatalog.Key(kind, x.Name) == key || x.SourceKey == key)) ||
            (!string.IsNullOrWhiteSpace(sourceKey) && (x.SourceKey == sourceKey || OperationalMasterCatalog.Key(kind, x.Name) == sourceKey)) ||
            (kind == "equipos" && plateKey.Length > 0 && PlateKey(x.Plate) == plateKey) ||
            (kind == "gondolas" && !string.IsNullOrWhiteSpace(name) && PlateKey(x.Name) == PlateKey(name)))
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static string PlateKey(string? value) => new((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
