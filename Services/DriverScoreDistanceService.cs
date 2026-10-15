using ITServiceDeskApp.ViewModels.Operaciones;
using System.Globalization;
using System.Text;
namespace ITServiceDeskApp.Services;
public static class DriverScoreDistanceService
{
    public static decimal Estimate(IEnumerable<SeguimientoToneladasRowViewModel> rows, string driver, DateTime? from, DateTime? to, IEnumerable<string>? knownDrivers = null)
    {
        var periodRows = rows.Where(r => r.FechaEvento.HasValue &&
            (!from.HasValue || r.FechaEvento.Value.Date >= from.Value.Date) &&
            (!to.HasValue || r.FechaEvento.Value.Date <= to.Value.Date)).ToList();
        var target = Normalize(driver);
        if (string.IsNullOrEmpty(target) || target is "SIN CONDUCTOR" or "SIN ASIGNAR" or "N D") return 0m;
        var names = periodRows.Select(r => r.Conductor).Concat(knownDrivers ?? []).Append(driver).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(Tokens).Where(x => x.Count >= 2)
            .DistinctBy(x => string.Join(" ", x.OrderBy(t => t, StringComparer.Ordinal))).ToList();
        var targetTokens = Tokens(driver);
        bool Matches(string candidate)
        {
            if (Normalize(candidate) == target) return true;
            var candidateTokens = Tokens(candidate);
            if (targetTokens.Count < 2 || candidateTokens.Count < 2) return false;
            var shortName = targetTokens.Count <= candidateTokens.Count ? targetTokens : candidateTokens;
            var longName = targetTokens.Count > candidateTokens.Count ? targetTokens : candidateTokens;
            if (!shortName.IsSubsetOf(longName)) return false;
            // Never resolve an abbreviation shared by two different full names.
            var candidates = names.Where(x => shortName.IsSubsetOf(x)).ToList();
            var identities = candidates.Where(x => !candidates.Any(y => x.IsProperSubsetOf(y))).ToList();
            return identities.Count == 1 && longName.IsSubsetOf(identities[0]);
        }
        var matches = periodRows.Select(r => r.Conductor).Distinct().ToDictionary(x => x, Matches);
        return periodRows.Where(r => matches[r.Conductor]).Sum(r => Normalize(r.Estado).Replace(" ", "") switch
        { "FINALIZADO" => 520m, "ENRUTA" => 260m, _ => 0m });
    }
    private static HashSet<string> Tokens(string? name) => Normalize(name).Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(x => x is not ("DE" or "DEL" or "LA" or "LAS" or "LOS" or "Y"))
        .ToHashSet(StringComparer.Ordinal);
    private static string Normalize(string? name)
    {
        var plain = new string((name ?? "").Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ').ToArray());
        return string.Join(" ", plain.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
