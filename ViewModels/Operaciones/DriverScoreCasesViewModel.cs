using ITServiceDeskApp.Models;
namespace ITServiceDeskApp.ViewModels.Operaciones;
public sealed record DriverScoreCasesViewModel(string? Period, string? Driver, IReadOnlyList<DriverScoreEventViewModel> Events, IReadOnlyDictionary<string, DriverScoreCase> Cases) { public DateTime? From { get; init; } public DateTime? To { get; init; } }
