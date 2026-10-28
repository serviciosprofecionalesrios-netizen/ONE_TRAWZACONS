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
    public string BaseStatus { get; set; } = "standby";
    public string Status { get; set; } = "standby";
    public string? Reason { get; set; }
    public string? ManualStateKey { get; set; }
    public List<string> ManualStateKeys { get; set; } = [];
    public bool InLatestUpload { get; set; }
    public string LoadVersion { get; set; } = "none";
    public bool CanManage { get; set; }
}
