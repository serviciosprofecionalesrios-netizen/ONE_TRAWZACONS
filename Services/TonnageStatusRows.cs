using ITServiceDeskApp.ViewModels.Operaciones;

namespace ITServiceDeskApp.Services;

public static class TonnageStatusRows
{
    public static List<SeguimientoToneladasRowViewModel> Select(IEnumerable<SeguimientoToneladasRowViewModel> source, DateTime? operationalDate)
    {
        var rows = source.ToList();
        var day = operationalDate?.Date;
        var eligible = rows.Where(x => !day.HasValue || !x.FechaEvento.HasValue || x.FechaEvento.Value.Date <= day.Value).ToList();
        var open = eligible.Where(x => !string.IsNullOrWhiteSpace(x.Equipo))
            .GroupBy(x => x.Equipo.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.FechaEvento ?? DateTime.MinValue)
                .ThenByDescending(x => rows.IndexOf(x)).First())
            .Where(x => IsOpen(x.Estado));
        // Keep daily discharge events for production totals, but only the latest
        // pending load per truck, including undated and previous-month loads.
        var current = eligible.Where(x => !IsOpen(x.Estado) && (!day.HasValue || x.FechaEvento?.Date == day.Value));
        return current.Concat(open).ToList();
    }

    private static bool IsOpen(string? state)
    {
        var value = (state ?? "").ToUpperInvariant().Replace(" ", "");
        if (new[] { "FINALIZAD", "DESCARGAD", "COMPLETAD", "TERMINAD", "CERRAD" }.Any(value.Contains)) return false;
        return new[] { "ENRUTA", "ENTRADA", "ENCAMINO", "RUTA", "TRANSITO", "TRÁNSITO", "PROCESO", "PENDIENTE" }.Any(value.Contains);
    }
}
