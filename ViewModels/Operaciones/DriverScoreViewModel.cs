namespace ITServiceDeskApp.ViewModels.Operaciones;

public sealed class DriverScoreViewModel
{
    public bool UsingDateRange { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? SelectedDriver { get; init; }
    public string SelectedPeriod { get; init; } = string.Empty;
    public string? Warning { get; init; }
    public string? DistanceWarning { get; init; }
    public DateTimeOffset? RetrievedAt { get; init; }
    public int TotalEvents { get; init; }
    public int DriversWithEvents { get; init; }
    public int TimelyManaged { get; init; }
    public int CoachingCompleted { get; init; }
    public IReadOnlyList<string> Drivers { get; init; } = [];
    public IReadOnlyList<DriverScorePeriodOptionViewModel> Periods { get; init; } = [];
    public IReadOnlyList<DriverScoreEventViewModel> Events { get; init; } = [];
    public IReadOnlyList<DriverScoreEventViewModel> AllPeriodEvents { get; init; } = [];
    public IReadOnlyList<DriverScoreHeatCellViewModel> HeatMap { get; init; } = [];
    public IReadOnlyList<DriverScoreRankingViewModel> Rankings { get; init; } = [];
    public IReadOnlyList<DriverScoreMapPointViewModel> MapPoints { get; init; } = [];
    public IReadOnlyList<DriverScoreEventDistributionViewModel> EventDistribution { get; init; } = [];
    public IReadOnlyList<DriverScoreMonthlyEventViewModel> MonthlyEvents { get; init; } = [];
    public IReadOnlyList<DriverScoreSummaryViewModel> Scorecards { get; init; } = [];
    public IReadOnlyList<DriverScoreEventViewModel> History { get; init; } = [];
    public int PreviousMonthEvents { get; init; }
    public int CriticalDrivers => Scorecards.Count(x => x.Status.Contains("Detener", StringComparison.OrdinalIgnoreCase));
    public int CoachingPending => TotalEvents - CoachingCompleted;
    public decimal TimelyPercent => TotalEvents == 0 ? 0 : Math.Round(100m * TimelyManaged / TotalEvents, 1);
    public decimal? MonthChange => PreviousMonthEvents == 0 ? null : Math.Round(100m * (TotalEvents - PreviousMonthEvents) / PreviousMonthEvents, 1);
    public IReadOnlyList<DriverScoreRepeatViewModel> RepeatedAfterCoaching { get; init; } = [];
    public IReadOnlyDictionary<string, ITServiceDeskApp.Models.DriverScoreCase> Cases { get; init; } = new Dictionary<string, ITServiceDeskApp.Models.DriverScoreCase>();
    public string CaseStatus(DriverScoreEventViewModel e) => Cases.TryGetValue(e.CaseKey, out var c) ? c.Status : e.Coaching ? "En seguimiento" : "Pendiente";
    public int PendingCases => AllPeriodEvents.Count(e => CaseStatus(e) == "Pendiente");
    public int FollowingCases => AllPeriodEvents.Count(e => CaseStatus(e) == "En seguimiento");
    public int ClosedCases => AllPeriodEvents.Count(e => CaseStatus(e) == "Cerrado");
}

public sealed record DriverScoreRepeatViewModel(string Driver, string Vehicle, string EventType, int Count, DateTime CoachingAt);

public sealed record DriverScoreEventViewModel(DateTime? Date, string EventType, string Driver, string Vehicle, string Group, string Location, string Observation, bool Timely, bool Coaching)
{
    public int? ManualId { get; init; }
    public bool HasEvidence { get; init; }
    public string CaseKey => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { Date, EventType, Driver, Vehicle, ManualId }))));
}
public sealed record DriverScorePeriodOptionViewModel(string Value, string Label);
public sealed record DriverScoreHeatCellViewModel(string Driver, string EventType, int Count, int Level);
public sealed record DriverScoreRankingViewModel(string Driver, int Events, int Timely, int Coaching, string Vehicle);
public sealed record DriverScoreMapPointViewModel(decimal Latitude, decimal Longitude, string Driver, string EventType, string Vehicle, DateTime? Date, string Observation);
public sealed record DriverScoreEventDistributionViewModel(string EventType, int Count, string Vehicles) { public IReadOnlyList<string> Involved { get; init; } = []; }
public sealed record DriverScoreMonthlyEventViewModel(int Year, int Month, string EventType, int Count);
public sealed record DriverScoreSummaryViewModel(string Driver, string Vehicle, decimal Kilometers, decimal Events, int Score, string Status);
