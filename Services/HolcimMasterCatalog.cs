using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using System.Text.Json;
namespace ITServiceDeskApp.Services;

public static class HolcimMasterCatalog
{
    public static readonly string[] Companies = ["EQUINOX", "HOLCIM", "HOLCIM/EQUINOX"];
    public const string CompanyColumn = "Empresa de operación";
    public static string MergeCompanies(string current, string incoming) => current == incoming ? current : "HOLCIM/EQUINOX";
    public static string Identifier(string? value) => new((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    public static List<HolcimMasterSource> ReadSeed()
    {
        using var stream = typeof(HolcimMasterCatalog).Assembly.GetManifestResourceStream("ITServiceDeskApp.Resources.HolcimMastersSeed.json") ?? throw new InvalidOperationException("No se encontró el catálogo Holcim.");
        return JsonSerializer.Deserialize<List<HolcimMasterSource>>(stream) ?? [];
    }
    public static HolcimSourceDetail? Detail(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<HolcimSourceDetail>(json);

    // Match only within a master kind, by source reference, full normalized name, or exact plate/code.
    // Never use partial names, brand/model descriptions, or numeric-only plate guesses.
    public static List<OperationalMasterRecord> Matches(IEnumerable<OperationalMasterRecord> rows, HolcimMasterSource source)
    {
        var nameKey = OperationalMasterCatalog.Key(source.Kind, source.Name);
        var identifiers = source.Identifiers.Select(Identifier).Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal);
        return rows.Where(row => row.Kind == source.Kind &&
            (string.Equals(row.HolcimReference, source.Reference, StringComparison.OrdinalIgnoreCase) ||
             OperationalMasterCatalog.Key(row.Kind, row.Name) == nameKey || row.SourceKey == nameKey ||
             (row.Kind != "conductores" && (identifiers.Contains(Identifier(row.Name)) || identifiers.Contains(Identifier(row.Plate)))))).ToList();
    }

    public static HolcimImportResult Seed(ApplicationDbContext context, ILogger? logger = null, IEnumerable<HolcimMasterSource>? sources = null)
    {
        var rows = context.OperationalMasterRecords.ToList();
        var result = new HolcimImportResult();
        foreach (var source in sources ?? ReadSeed())
        {
            // A stable reference prevents re-creating a renamed or deactivated imported record.
            if (rows.Any(x => x.Kind == source.Kind && x.HolcimReference == source.Reference)) continue;
            var matches = Matches(rows, source);
            if (matches.Count > 1 || matches.Any(x => !string.IsNullOrWhiteSpace(x.HolcimReference) && x.HolcimReference != source.Reference))
            {
                result.Ambiguous++;
                logger?.LogWarning("Maestro Holcim {Kind} {Reference}: varias coincidencias; no se fusionó ni duplicó el registro.", source.Kind, source.Reference);
                continue;
            }
            var row = matches.SingleOrDefault();
            if (row == null)
            {
                row = new OperationalMasterRecord { Kind = source.Kind, Name = source.Name, Plate = source.Plate,
                    CompanyClassification = "HOLCIM", Tenure = "Pendiente de completar", Owner = "Pendiente de completar",
                    RecordKey = OperationalMasterCatalog.Key(source.Kind, source.Name), SourceKey = $"holcim:{source.Kind}:{source.Reference}" };
                context.OperationalMasterRecords.Add(row); rows.Add(row); result.Added++;
            }
            else
            {
                row.CompanyClassification = MergeCompanies(row.CompanyClassification, "HOLCIM");
                result.Linked++;
            }
            // Preserve the workbook's original names, dates, training records and inspection fields.
            row.HolcimReference = source.Reference;
            row.HolcimSourceJson = JsonSerializer.Serialize(source.Detail);
        }
        context.SaveChanges();
        return result;
    }
}
public sealed class HolcimMasterSource
{
    public string Kind { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Plate { get; set; }
    public List<string> Identifiers { get; set; } = [];
    public HolcimSourceDetail Detail { get; set; } = new();
}
public sealed class HolcimSourceDetail
{
    public string File { get; set; } = "";
    public string Sheet { get; set; } = "";
    public List<HolcimSourceRow> Rows { get; set; } = [];
}
public sealed class HolcimSourceRow
{
    public int Row { get; set; }
    public Dictionary<string, string?> Values { get; set; } = [];
}
public sealed class HolcimImportResult
{
    public int Added { get; set; }
    public int Linked { get; set; }
    public int Ambiguous { get; set; }
}
