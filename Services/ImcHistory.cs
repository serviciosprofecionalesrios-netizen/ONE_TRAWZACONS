using System.Globalization;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels.Operaciones;

namespace ITServiceDeskApp.Services;

public static class ImcHistory
{
    private sealed record Inspection(string Unit, DateTime PresentedAt, string Color);
    private static readonly Lazy<List<Inspection>> Inspections = new(ReadInspections);
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-NI");

    public static ImcMonthViewModel ForMonth(string? requestedMonth, IEnumerable<OperationalImcClearance>? clearances = null)
    {
        var periods = Inspections.Value.ToLookup(row => row.PresentedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        var months = periods.Select(period => period.Key).OrderDescending(StringComparer.Ordinal).ToArray();
        var selected = requestedMonth is not null && periods.Contains(requestedMonth) ? requestedMonth : months.FirstOrDefault();
        var units = periods[selected ?? ""]
            .GroupBy(row => HolcimMasterCatalog.Identifier(row.Unit))
            .Select(group => group.OrderByDescending(row => row.PresentedAt).First())
            .ToDictionary(row => HolcimMasterCatalog.Identifier(row.Unit));
        foreach (var clearance in clearances ?? [])
        {
            if (clearance.Month == selected)
                units[clearance.UnitKey] = new Inspection(clearance.UnitCode, default, clearance.Color);
        }
        return new ImcMonthViewModel
        {
            SelectedMonth = selected,
            Months = months.Select(month => new ImcMonthOption(month,
                DateTime.ParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture).ToString("MMMM yyyy", Spanish))).ToArray(),
            Cards = new (string Source, string Label, string Css)[] { ("Rojo", "Roja", "roja"), ("Amarillo", "Amarilla", "amarilla"), ("Verde", "Verde", "verde") }
                .Select(color => new ImcColorCard(color.Label, color.Css,
                    units.Values.Where(row => row.Color == color.Source).Select(row => row.Unit).Order(StringComparer.Ordinal).ToArray()))
                .ToArray()
        };
    }

    private static List<Inspection> ReadInspections()
    {
        // Snapshot of HistoryChecklist (3).xlsx, keeping only vehicle, submission time and result.
        using var stream = typeof(ImcHistory).Assembly.GetManifestResourceStream("ITServiceDeskApp.Resources.ImcHistory.tsv")
            ?? throw new InvalidOperationException("No se encontró el historial IMC.");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        var rows = new List<Inspection>();
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t');
            rows.Add(new Inspection(fields[0],
                DateTime.ParseExact(fields[1], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), fields[2]));
        }
        return rows;
    }
}
