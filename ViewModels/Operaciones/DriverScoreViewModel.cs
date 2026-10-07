namespace ITServiceDeskApp.ViewModels.Operaciones;

public sealed class DriverScoreViewModel
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? SelectedDriver { get; init; }
    public string? Warning { get; init; }
    public DateTimeOffset? RetrievedAt { get; init; }
    public int TotalEvents { get; init; }
    public int DriversWithEvents { get; init; }
    public int TimelyManaged { get; init; }
    public int CoachingCompleted { get; init; }
    public IReadOnlyList<string> Drivers { get; init; } = [];
    public IReadOnlyList<DriverScoreEventViewModel> Events { get; init; } = [];
    public IReadOnlyList<DriverScoreHeatCellViewModel> HeatMap { get; init; } = [];
    public IReadOnlyList<DriverScoreRankingViewModel> Rankings { get; init; } = [];
    public IReadOnlyList<DriverScoreSummaryViewModel> Scorecards { get; init; } = [];
}

public sealed record DriverScoreEventViewModel(DateTime? Date, string EventType, string Driver, string Vehicle, string Group, string Location, string Observation, bool Timely, bool Coaching);
public sealed record DriverScoreHeatCellViewModel(string Driver, string EventType, int Count, int Level);
public sealed record DriverScoreRankingViewModel(string Driver, int Events, int Timely, int Coaching, string Vehicle);
public sealed record DriverScoreSummaryViewModel(string Driver, string Vehicle, decimal Kilometers, decimal Events, string Status);
