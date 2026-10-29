using ITServiceDeskApp.Models;
using ITServiceDeskApp.Security;
using ITServiceDeskApp.ViewModels.Operaciones;
using System.Globalization;
using System.Security.Claims;
namespace ITServiceDeskApp.Services;
public static class OperationalFleetBoard
{
    public static readonly string[] Reasons = ["Plantel Contratista", "Otros", "Viajes Externos", "Conductor de Pase"];
    public static readonly string[] ManualStatuses = ["auto", "finalizado", "fuera-ruta", "carretera", "taller-pct", "taller-psb", "taller-poz"];
    public static bool CanManage(ClaimsPrincipal user) => UserAccessProfiles.CanMaintainDrivers(user) || UserAccessProfiles.CanMaintainOperationalMasters(user);
    public static string Version(DateTime? uploadedAt) => uploadedAt?.ToString("O", CultureInfo.InvariantCulture) ?? "none";
    public static string StatusLabel(string status) => status switch {
        "finalizado" => "Equipos descargados", "ruta" => "Equipos en ruta", "standby" => "Equipos en Standby",
        "fuera-ruta" => "Equipos Fuera de Ruta", "carretera" => "Varado en Carretera",
        "taller" => "En Taller", "taller-pct" => "En Taller · PCT", "taller-psb" => "En Taller · PSB", "taller-poz" => "En Taller · POZ", _ => "Otros estados del archivo"
    };
    public static FleetBoardViewModel Build(IEnumerable<OperationalMasterRecord> catalog, SeguimientoToneladasViewModel source, IEnumerable<OperationalFleetState> changes, IEnumerable<OperationalFleetDriver>? assignments = null)
    {
        var records = catalog.ToList();
        var masters = records.Where(x => x.Kind == "equipos" && x.IsActive).ToList();
        var board = new FleetBoardViewModel { HasUpload = source.IsFromUpload && source.LatestUploadAt.HasValue, LoadVersion = Version(source.LatestUploadAt) };
        var byKey = masters.ToDictionary(x => "master:" + x.Id, x => new FleetEquipmentViewModel {
            Key = "master:" + x.Id, Code = x.Name, Company = x.CompanyClassification, MasterDisplayId = x.DisplayId, LoadVersion = board.LoadVersion });
        HashSet<string> currentKeys = board.HasUpload ? source.LatestUploadEquipment.Select(HolcimMasterCatalog.Identifier).Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal) : [];
        var fleetRows = source.LatestUploadRows.Count > 0 ? source.LatestUploadRows : source.Rows;
        var groupedRows = fleetRows.Select((row,index) => new { row,index }).Where(x => currentKeys.Contains(HolcimMasterCatalog.Identifier(x.row.Equipo)))
            .Select(x => {
                var id = HolcimMasterCatalog.Identifier(x.row.Equipo);
                var matches = masters.Where(m => HolcimMasterCatalog.Identifier(m.Name) == id || (!string.IsNullOrWhiteSpace(m.Plate) && HolcimMasterCatalog.Identifier(m.Plate) == id)).ToList();
                return new { x.row, x.index, Key = matches.Count == 1 ? "master:" + matches[0].Id : "unit:" + id };
            }).GroupBy(x => x.Key);
        foreach (var group in groupedRows)
        {
            var last = group.OrderByDescending(x => x.row.FechaEvento).ThenByDescending(x => x.index).First().row;
            if (!byKey.TryGetValue(group.Key,out var equipment))
            {
                equipment = new FleetEquipmentViewModel { Key = group.Key, Code = last.Equipo, LoadVersion = board.LoadVersion };
                byKey[group.Key] = equipment;
            }
            equipment.InLatestUpload = true;
            equipment.Driver = last.Conductor;
            equipment.Status = equipment.BaseStatus = MapState(last.Estado);
        }
        // A manual classification always wins, across uploads and dates, until a user clears it.
        foreach (var change in changes.Where(x => ManualStatuses.Contains(x.Status) && x.Status != "auto").OrderBy(x => x.UpdatedAtUtc))
        {
            if (!byKey.TryGetValue(change.EquipmentKey,out var equipment))
            {
                var code = HolcimMasterCatalog.Identifier(change.EquipmentCode);
                var matches = masters.Where(m => HolcimMasterCatalog.Identifier(m.Name) == code || (!string.IsNullOrWhiteSpace(m.Plate) && HolcimMasterCatalog.Identifier(m.Plate) == code)).ToList();
                if (change.EquipmentKey.StartsWith("unit:") && matches.Count == 1) equipment = byKey["master:" + matches[0].Id];
                else if (change.EquipmentKey.StartsWith("unit:") && !string.IsNullOrWhiteSpace(change.EquipmentCode))
                {
                    equipment = new FleetEquipmentViewModel {Key=change.EquipmentKey,Code=change.EquipmentCode,LoadVersion=board.LoadVersion};
                    byKey[change.EquipmentKey] = equipment;
                }
                else continue;
            }
            // Manual activation supplies availability until TiHouse reports an actual trip.
            equipment.Status = change.Status == "finalizado" && equipment.InLatestUpload && source.LatestUploadAt != change.SourceUploadedAt && equipment.BaseStatus is "ruta" or "finalizado"
                ? equipment.BaseStatus : change.Status;
            equipment.Reason = change.Reason;
            equipment.ManualStateKey = change.EquipmentKey;
            equipment.ManualStateKeys.Add(change.EquipmentKey);
        }
        foreach (var assignment in (assignments ?? []).OrderBy(x => x.UpdatedAtUtc))
        {
            var equipment = byKey.GetValueOrDefault(assignment.EquipmentKey);
            if (equipment == null && assignment.EquipmentKey.StartsWith("unit:"))
            {
                var code = HolcimMasterCatalog.Identifier(assignment.EquipmentCode);
                var matches = masters.Where(m => HolcimMasterCatalog.Identifier(m.Name) == code ||
                    (!string.IsNullOrWhiteSpace(m.Plate) && HolcimMasterCatalog.Identifier(m.Plate) == code)).ToList();
                if (matches.Count == 1) equipment = byKey["master:" + matches[0].Id];
            }
            var driver = records.SingleOrDefault(x => x.Id == assignment.DriverId && x.Kind == "conductores" && x.IsActive);
            if (equipment != null && driver != null)
            {
                equipment.Driver = driver.Name;
                equipment.AssignedDriverId = driver.Id;
            }
        }
        board.Equipment = byKey.Values.OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToList();
        return board;
    }
    private static string MapState(string? value)
    {
        var state = OperationalMasterCatalog.Key("",value ?? "").Replace(" ","");
        if (new[] {"FINALIZAD","DESCARGAD","COMPLETAD","TERMINAD","CERRAD"}.Any(state.Contains)) return "finalizado";
        if (state.Contains("FUERADERUTA")) return "fuera-ruta";
        if (state.Contains("VARAD") || state.Contains("CARRETERA")) return "carretera";
        if (state.Contains("TALLER")) return state.Contains("PSB") ? "taller-psb" : state.Contains("POZ") ? "taller-poz" : "taller-pct";
        if (new[] {"ENRUTA","ENTRADA","ENCAMINO","RUTA","TRANSITO","PROCESO","PENDIENTE"}.Any(state.Contains)) return "ruta";
        return "standby";
    }
}
