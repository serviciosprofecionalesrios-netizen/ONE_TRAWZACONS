using System.Text.Json;
using ITServiceDeskApp.Models;
namespace ITServiceDeskApp.Services;
public static class MaintenanceBreakdownBoard
{
    private static Dictionary<string,string> Metadata(OperationalBreakdown record)
    {
        if(string.IsNullOrWhiteSpace(record.SourceJson)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string,string>>(record.SourceJson) ?? []; }
        catch(JsonException) { return []; }
    }
    public static bool IsOperationallyOpen(OperationalBreakdown record)
    {
        if(record.EndAt!=null || !string.Equals(record.Status.Trim(),"Abierta",StringComparison.OrdinalIgnoreCase)) return false;
        var values=Metadata(record);
        var validity=values.GetValueOrDefault("VIGENCIA")?.Trim();
        if(string.Equals(validity,"NULO",StringComparison.OrdinalIgnoreCase)) return false;
        var sourceStatus=values.GetValueOrDefault("Estatus")?.Trim();
        if(string.Equals(sourceStatus,"Cerrada",StringComparison.OrdinalIgnoreCase)) return false;
        return OperationalBreakdownCatalog.Date(values.GetValueOrDefault("Fecha Fin Evento") ?? "")==null;
    }
    public sealed record Row(int Id, DateTime EventAt, string Equipment, string? Driver, string Site, string Category, string Element, string DamageTarget, string? Observation, double ElapsedHours);
    public static List<Row> Build(IEnumerable<OperationalBreakdown> records, IEnumerable<OperationalFleetState> fleetStates, DateTime nowUtc)
    {
        var states = fleetStates.OrderBy(x=>x.UpdatedAtUtc).GroupBy(x=>HolcimMasterCatalog.Identifier(x.EquipmentCode))
            .ToDictionary(x=>x.Key,x=>x.Last().Status);
        return records.Where(IsOperationallyOpen)
            .Select(x=> {
                var site=Metadata(x).GetValueOrDefault("Sitio")?.Trim() ?? "";
                if(site.Length==0 && states.TryGetValue(HolcimMasterCatalog.Identifier(x.Equipment),out var state))
                    site=state switch {"carretera"=>"Carretera","taller-pct"=>"PCT","taller-psb"=>"PSB","taller-poz"=>"POZ",_=>""};
                if(site.Length==0) site="Sin ubicación registrada";
                // Breakdown event times are stored in Nicaragua local time (UTC-6).
                var elapsed=Math.Max(0,(DateTime.SpecifyKind(nowUtc,DateTimeKind.Unspecified).AddHours(-6)-DateTime.SpecifyKind(x.EventAt,DateTimeKind.Unspecified)).TotalHours);
                return new Row(x.Id,x.EventAt,x.Equipment,x.Driver,site,x.Category,x.Element,x.DamageTarget,x.Observation,elapsed);
            }).OrderBy(x=>x.EventAt).ThenBy(x=>x.Id).ToList();
    }
}
