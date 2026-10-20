using ITServiceDeskApp.Models;
using ITServiceDeskApp.Data;
using System.Text.Json;
using System.Text;
using System.Globalization;
namespace ITServiceDeskApp.Services;
public static class OperationalMasterCatalog
{
    public static readonly string[] Kinds = ["equipos", "gondolas", "conductores"];
    public static string Title(string kind) => kind switch { "conductores" => "Maestro de Conductores", "gondolas" => "Maestro de Góndolas", _ => "Maestros de Equipos" };
    public static string NameLabel(string kind) => kind switch { "conductores" => "Conductor", "gondolas" => "Góndola", _ => "Cabezal" };
    public static string Key(string kind, string name) => kind + ":" + new string(name.Trim().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
    public static List<OperationalMasterRecord> ReadSeed()
    {
        using var stream = typeof(OperationalMasterCatalog).Assembly.GetManifestResourceStream("ITServiceDeskApp.Resources.OperationalMastersSeed.json") ?? throw new InvalidOperationException("No se encontró el catálogo inicial de maestros.");
        return JsonSerializer.Deserialize<List<OperationalMasterRecord>>(stream) ?? [];
    }
    public static List<OperationalMasterRecord> MissingRecords(IEnumerable<string> existingKeys)
    {
        var keys = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = new List<OperationalMasterRecord>();
        foreach(var row in ReadSeed())
        {
            row.RecordKey = Key(row.Kind, row.Name);
            if (keys.Add(row.RecordKey)) missing.Add(row);
        }
        return missing;
    }
    public static void Seed(ApplicationDbContext context)
    {
        context.OperationalMasterRecords.AddRange(MissingRecords(context.OperationalMasterRecords.Select(x => x.RecordKey).ToList()));
        context.SaveChanges();
    }
}
