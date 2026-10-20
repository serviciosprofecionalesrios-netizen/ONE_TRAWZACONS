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
    public static string[] Columns(string kind) => kind switch
    {
        "equipos" => ["Cabezal", "Placa de cabezal", "Tenencia", "Propietario"],
        "gondolas" => ["Tenencia", "Propietario", "Gondola"],
        _ => ["Primer nombre", "Segundo nombre", "Primer apellido", "Segundo apellido", "Tenencia", "Propietario"]
    };
    public static OperationalMasterRecord FromValues(string kind, string[] values) => kind switch
    {
        "equipos" => new() { Kind = kind, Name = values[0], Plate = values[1], Tenure = values[2], Owner = values[3] },
        "gondolas" => new() { Kind = kind, Name = values[2], Tenure = values[0], Owner = values[1] },
        _ when values.Length == 6 => new() { Kind = kind, FirstName = values[0], SecondName = values[1], FirstSurname = values[2], SecondSurname = values[3], Name = string.Join(" ", values.Take(4)), Tenure = values[4], Owner = values[5] },
        _ => new() { Kind = kind, Name = values[0], Tenure = values[1], Owner = values[2] }
    };
    public static string Key(string kind, string name) => kind + ":" + new string(NormalizeName(name).Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
    public static string NormalizeName(string? value) => string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    public static string? PrepareDriver(OperationalMasterRecord row)
    {
        row.FirstName = NormalizeName(row.FirstName); row.SecondName = NormalizeName(row.SecondName);
        row.FirstSurname = NormalizeName(row.FirstSurname); row.SecondSurname = NormalizeName(row.SecondSurname);
        var parts = new[] { row.FirstName, row.SecondName, row.FirstSurname, row.SecondSurname };
        if (parts.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 40 || !x.Any(char.IsLetter) || x.Any(c => !char.IsLetter(c) && c != ' ' && c != '-' && c != '\'' && c != '’')))
            return "Completa los dos nombres y los dos apellidos, usando únicamente letras, espacios, apóstrofos o guiones (hasta 40 caracteres por campo).";
        row.Name = string.Join(" ", parts);
        if (row.Name.Length > 160) return "El nombre completo no puede superar 160 caracteres.";
        return null;
    }
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
            row.SourceKey = row.RecordKey;
            if (keys.Add(row.RecordKey)) missing.Add(row);
        }
        return missing;
    }
    public static void Seed(ApplicationDbContext context)
    {
        context.OperationalMasterRecords.AddRange(MissingRecords(context.OperationalMasterRecords.Select(x => x.RecordKey).ToList().Concat(context.OperationalMasterRecords.Where(x => x.SourceKey != null).Select(x => x.SourceKey!).ToList())));
        context.SaveChanges();
    }
}
