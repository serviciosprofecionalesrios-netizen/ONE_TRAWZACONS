using ITServiceDeskApp.ViewModels.Operaciones;

namespace ITServiceDeskApp.Services;

public static class TonnageStatusRows
{
    public static List<SeguimientoToneladasRowViewModel> Select(IEnumerable<SeguimientoToneladasRowViewModel> source, DateTime? operationalDate)
    {
        var rows = source.ToList();
        if (!operationalDate.HasValue) return rows;
        var day = operationalDate.Value.Date;
        var current = rows.Where(x => x.FechaEvento?.Date == day).ToList();
        var currentEquipment = current.Select(x => x.Equipo.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var carried = rows.Where(x => x.FechaEvento.HasValue && x.FechaEvento.Value.Date < day && !string.IsNullOrWhiteSpace(x.Equipo))
            .GroupBy(x => x.Equipo.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.FechaEvento).First())
            .Where(x => !currentEquipment.Contains(x.Equipo.Trim()) && IsOpen(x.Estado));
        return current.Concat(carried).ToList();
    }

    private static bool IsOpen(string? state)
    {
        var value = (state ?? "").ToUpperInvariant().Replace(" ", "");
        if (new[] { "FINALIZAD", "DESCARGAD", "COMPLETAD", "TERMINAD", "CERRAD" }.Any(value.Contains)) return false;
        return new[] { "ENRUTA", "ENTRADA", "ENCAMINO", "RUTA", "TRANSITO", "TRÁNSITO", "PROCESO", "PENDIENTE" }.Any(value.Contains);
    }
}
