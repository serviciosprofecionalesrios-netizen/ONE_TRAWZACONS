using System.Globalization;
using System.Text.Json;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
namespace ITServiceDeskApp.Services;
public static class OperationalBreakdownCatalog
{
    public const string SourceUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vQ8bvMchQDTW_PGEa0Yc3rtpZU7GPJ5fKSTx6iuixRZstSTC71HkEfSXjDRGJqcBq-XYibb9byfMqAl/pubhtml";
    private static readonly Lazy<List<SourceRow>> Source = new(() => {
        using var stream = typeof(OperationalBreakdownCatalog).Assembly.GetManifestResourceStream("ITServiceDeskApp.Resources.AveriasSeed.json")
            ?? throw new InvalidOperationException("No se encontró la bitácora de averías.");
        return JsonSerializer.Deserialize<List<SourceRow>>(stream) ?? [];
    });
    public static List<SourceRow> Rows => Source.Value;
    public static Dictionary<string,string[]> Elements => Rows.GroupBy(x => x.Get("Categoria (Daño)"))
        .Where(x => x.Key.Length > 0).ToDictionary(x => x.Key,x => x.Select(r => r.Get("Elementos  (Daño)")).Where(v => v.Length > 0).Distinct().OrderBy(v => v).ToArray());
    public static string[] Options(string field) => Rows.Select(x => x.Get(field)).Where(x => x.Length > 0).Distinct().OrderBy(x => x).ToArray();
    public static DateTime? Date(string value)
    {
        // One historical row contains 18//08/2025; retain the raw value in SourceJson.
        var normalized = System.Text.RegularExpressions.Regex.Replace(value.Trim(),"/{2,}","/");
        return DateTime.TryParseExact(normalized,new[]{"d/M/yyyy H:mm:ss","d/M/yyyy H:mm","d/M/yyyy"},CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed) ? parsed : null;
    }
    public static List<OperationalBreakdown> Missing(IEnumerable<string?> existing)
    {
        var keys = existing.Where(x => x != null).ToHashSet(StringComparer.Ordinal);
        var result = new List<OperationalBreakdown>();
        foreach(var row in Rows)
        {
            if(!keys.Add(row.SourceKey)) continue;
            var date = Date(row.Get("Fecha Inicio Evento"));
            if(date == null) throw new InvalidOperationException($"Fecha no válida en {row.SourceKey}.");
            result.Add(new OperationalBreakdown { SourceKey=row.SourceKey,EventAt=date.Value,Equipment=row.Get("Equipo"),Driver=row.Get("Conductor"),
                Circuit=row.Get("Circuito"),EquipmentStatus=row.Get("Estatus Equipo"),Category=row.Get("Categoria (Daño)"),Element=row.Get("Elementos  (Daño)"),
                DamageTarget=row.Get("DAÑO EN EQUIPO"),Observation=row.Get("Observación del Daño"),Status=row.Get("Estatus"),EndAt=Date(row.Get("Fecha Fin Evento")),
                RegisteredBy=row.Get("Usuario"),SourceJson=JsonSerializer.Serialize(row.Values) });
        }
        return result;
    }
    public static void Seed(ApplicationDbContext context)
    {
        var missing = Missing(context.OperationalBreakdowns.Select(x => x.SourceKey).ToList());
        if(missing.Count == 0) return;
        context.OperationalBreakdowns.AddRange(missing);
        context.SaveChanges();
    }
    public sealed class SourceRow
    {
        public string SourceKey { get; set; } = "";
        public Dictionary<string,string> Values { get; set; } = [];
        public string Get(string key) => Values.GetValueOrDefault(key)?.Trim() ?? "";
    }
}
