namespace ITServiceDeskApp.ViewModels.Operaciones;
public sealed class FleetBoardViewModel
{
    public string LoadVersion { get; set; } = "none";
    public bool HasUpload { get; set; }
    public bool CanManage { get; set; }
    public List<FleetEquipmentViewModel> Equipment { get; set; } = [];
}
public sealed class FleetEquipmentViewModel
{
    public string Key { get; set; } = "";
    public string Code { get; set; } = "";
    public string Company { get; set; } = "";
    public string? MasterDisplayId { get; set; }
    public string Driver { get; set; } = "";
    public decimal? SuggestedWeight { get; set; }
    public decimal? CompletedTonnes { get; set; }
    public decimal? DispatchedFuelGallons { get; set; }
    public int? AssignedDriverId { get; set; }
    public List<ITServiceDeskApp.Models.OperationalMasterRecord> AvailableDrivers { get; set; } = [];
    public string BaseStatus { get; set; } = "standby";
    public string Status { get; set; } = "standby";
    public string HistoricalLoadStatus { get; set; } = "standby";
    // TiHouse determines the load; manual classifications determine its location.
    public string LoadStatus => BaseStatus is "ruta" or "finalizado" ? BaseStatus
        : HistoricalLoadStatus is "ruta" or "finalizado" ? HistoricalLoadStatus : Status;
    // Active cards follow the same load state as the chip. Manual locations stay put.
    public string CardStatus => Status is "standby" or "finalizado" or "ruta"
        && LoadStatus is "ruta" or "finalizado" ? LoadStatus : Status;
    public string ChipCssClass => Status == "imc-roja" ? "equipo-chip--imc-roja"
        : Status == "imc-amarilla" ? "equipo-chip--imc-amarilla"
        : LoadStatus == "ruta" ? "equipo-chip--ruta"
        : LoadStatus == "finalizado" ? "equipo-chip--descargado" : "";
    public string? Reason { get; set; }
    public string? ManualStateKey { get; set; }
    public List<string> ManualStateKeys { get; set; } = [];
    public bool InLatestUpload { get; set; }
    public string LoadVersion { get; set; } = "none";
    public bool CanManage { get; set; }
    public bool ShowsBreakdownDetails => Status is "carretera" or "taller-pct" or "taller-psb" or "taller-poz";
    public bool CanChangeStatus { get; set; }
    public List<FleetBreakdownViewModel> Breakdowns { get; set; } = [];
}
public sealed class FleetBreakdownViewModel
{
    public int Id { get; set; }
    public string Category { get; set; } = "";
    public string Element { get; set; } = "";
    public string DamageTarget { get; set; } = "";
    public string? Observation { get; set; }
    public DateTime ReportedAt { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool HasDuration { get; set; } = true;
    public string DurationLabel => IsClosed ? "Duración de la avería" : "Tiempo transcurrido";
    public TimeSpan Elapsed { get; set; }
    public string ElapsedLabel => HasDuration ? $"{(int)Elapsed.TotalDays} d {Elapsed.Hours} h {Elapsed.Minutes} min" : "Sin fecha de cierre válida";
    public long? ClosedAtUnixMs => ClosedAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(ClosedAt.Value,DateTimeKind.Unspecified),TimeSpan.FromHours(-6)).ToUnixTimeMilliseconds() : null;
    public long ReportedAtUnixMs => new DateTimeOffset(ReportedAt, TimeSpan.FromHours(-6)).ToUnixTimeMilliseconds();
    public string Details => $"Última avería registrada · {(IsClosed ? "Cerrada" : "Abierta")}\nDaño: {Category}\nElemento dañado: {Element}\nDaño en: {DamageTarget}\nReportado: {ReportedAt:dd/MM/yyyy HH:mm} (Nicaragua)"
        + (ClosedAt.HasValue ? $"\nCierre: {ClosedAt:dd/MM/yyyy HH:mm} (Nicaragua)" : "")
        + (string.IsNullOrWhiteSpace(Observation) ? "" : $"\nObservación: {Observation}");
}
