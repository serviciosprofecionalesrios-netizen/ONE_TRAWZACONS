using ITServiceDeskApp.ViewModels.Operaciones;
using System.Globalization;
using System.Text;
namespace ITServiceDeskApp.Services;
public static class DriverScoreDistanceService
{
    public static decimal Estimate(IEnumerable<SeguimientoToneladasRowViewModel> rows, string driver, DateTime? from, DateTime? to) =>
        rows.Where(r => r.FechaEvento.HasValue && (!from.HasValue || r.FechaEvento.Value.Date >= from.Value.Date) && (!to.HasValue || r.FechaEvento.Value.Date <= to.Value.Date) && Normalize(r.Conductor) == Normalize(driver))
            .Sum(r => r.Estado.Trim().Equals("Finalizado", StringComparison.OrdinalIgnoreCase) ? 520m : r.Estado.Trim().Equals("En ruta", StringComparison.OrdinalIgnoreCase) ? 260m : 0m);
    private static string Normalize(string name) => string.Join(" ", new string(name.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
