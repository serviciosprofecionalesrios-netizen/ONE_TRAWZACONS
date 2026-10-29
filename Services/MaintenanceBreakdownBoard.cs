using System.Text.Json;
using ITServiceDeskApp.Models;
namespace ITServiceDeskApp.Services;
public static class MaintenanceBreakdownBoard
{
    public sealed record Row(int Id, DateTime EventAt, string Equipment, string? Driver, string Site, string Category, string Element, string DamageTarget, string? Observation, double ElapsedHours);
    public static List<Row> Build(IEnumerable<OperationalBreakdown> records, IEnumerable<OperationalFleetState> fleetStates, DateTime nowUtc)
    {
        var states = fleetStates.OrderBy(x=>x.UpdatedAtUtc).GroupBy(x=>HolcimMasterCatalog.Identifier(x.EquipmentCode))
            .ToDictionary(x=>x.Key,x=>x.Last().Status);
        return records.Where(x=>x.EndAt==null && string.Equals(x.Status.Trim(),"Abierta",StringComparison.OrdinalIgnoreCase))
            .Select(x=> {
                var site="";
                if(!string.IsNullOrWhiteSpace(x.SourceJson))
                {
                    try { site=JsonSerializer.Deserialize<Dictionary<string,string>>(x.SourceJson)?.GetValueOrDefault("Sitio")?.Trim() ?? ""; }
                    catch(JsonException) { /* Missing location must not hide an otherwise open breakdown. */ }
                }
                if(site.Length==0 && states.TryGetValue(HolcimMasterCatalog.Identifier(x.Equipment),out var state))
                    site=state switch {"carretera"=>"Carretera","taller-pct"=>"PCT","taller-psb"=>"PSB","taller-poz"=>"POZ",_=>""};
                if(site.Length==0) site="Sin ubicación registrada";
                // Breakdown event times are stored in Nicaragua local time (UTC-6).
                var elapsed=Math.Max(0,(DateTime.SpecifyKind(nowUtc,DateTimeKind.Unspecified).AddHours(-6)-DateTime.SpecifyKind(x.EventAt,DateTimeKind.Unspecified)).TotalHours);
                return new Row(x.Id,x.EventAt,x.Equipment,x.Driver,site,x.Category,x.Element,x.DamageTarget,x.Observation,elapsed);
            }).OrderBy(x=>x.EventAt).ThenBy(x=>x.Id).ToList();
    }
}
