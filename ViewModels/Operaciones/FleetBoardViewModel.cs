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
    public int? AssignedDriverId { get; set; }
    public List<ITServiceDeskApp.Models.OperationalMasterRecord> AvailableDrivers { get; set; } = [];
    public string BaseStatus { get; set; } = "standby";
    public string Status { get; set; } = "standby";
    // TiHouse determines the load; manual classifications determine its location.
    public string LoadStatus => BaseStatus is "ruta" or "finalizado" ? BaseStatus : Status;
    public string ChipCssClass => LoadStatus == "ruta" ? "equipo-chip--ruta"
        : LoadStatus == "finalizado" ? "equipo-chip--descargado" : "";
    public string? Reason { get; set; }
    public string? ManualStateKey { get; set; }
    public List<string> ManualStateKeys { get; set; } = [];
    public bool InLatestUpload { get; set; }
    public string LoadVersion { get; set; } = "none";
    public bool CanManage { get; set; }
}
