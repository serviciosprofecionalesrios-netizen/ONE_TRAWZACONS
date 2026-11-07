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
    public static bool IsValidRecord(OperationalBreakdown record) =>
        !string.Equals(Metadata(record).GetValueOrDefault("VIGENCIA")?.Trim(),"NULO",StringComparison.OrdinalIgnoreCase);
    public static DateTime? ClosingDate(OperationalBreakdown record) => record.EndAt ??
        OperationalBreakdownCatalog.Date(Metadata(record).GetValueOrDefault("Fecha Fin Evento") ?? "");
    public static DateTime? Deadline(OperationalBreakdown record)
    {
        if(record.SlaDeadline.HasValue)return record.SlaDeadline;
        var raw=Metadata(record).GetValueOrDefault("T. Estimado Mantt.")?.Replace(',','.');
        if(double.TryParse(raw,System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out var hours) && hours>0 && hours<=87600)
            return record.EventAt.AddHours(hours);
        return null;
    }
    public sealed record Row(int Id, DateTime EventAt, string Equipment, string? Driver, string Site, string Category, string Element, string DamageTarget, string? Observation, double ElapsedHours)
    {
        public string Technician {get;init;}="Sin asignar";
        public bool IsUnassigned {get;init;}=true;
        public string RepairSummary { get; init; } = "—";
        public DateTime? SlaDeadline {get;init;}
        public double? RemainingHours {get;init;}
        public string SemaphoreLabel {get;init;}="Sin plazo";
        public string SemaphoreTone {get;init;}="secondary";
        public int SemaphoreRank {get;init;}=3;
    }
    public static List<Row> Build(IEnumerable<OperationalBreakdown> records, IEnumerable<OperationalFleetState> fleetStates, DateTime nowUtc,IEnumerable<MaintenanceTechnician>? technicians=null)
    {
        var technicianNames=(technicians ?? []).ToDictionary(x=>x.Id,x=>x.FullName);
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
                var deadline=Deadline(x);
                double? remaining=deadline.HasValue?(deadline.Value-DateTime.SpecifyKind(nowUtc,DateTimeKind.Unspecified).AddHours(-6)).TotalHours:null;
                var semaphore=remaining.HasValue?MaintenanceSemaphore.Evaluate(remaining.Value,TicketStatus.Open,null):("Sin plazo","secondary",3);
                var damages = x.GetDamages().ToArray();
                var techniciansAssigned = x.GetTechnicianIds();
                return new Row(x.Id,x.EventAt,x.Equipment,x.Driver,site,
                    string.Join(", ", damages.Select(d => d.Category).Distinct()),
                    string.Join(", ", damages.Select(d => d.Element).Distinct()),
                    string.Join(", ", damages.Select(d => d.Target).Distinct()),x.Observation,elapsed){
                    Technician=techniciansAssigned.Length == 0 ? "Sin asignar" :
                        string.Join(", ", techniciansAssigned.Select(id => technicianNames.GetValueOrDefault(id, $"Técnico #{id}"))),
                    IsUnassigned=techniciansAssigned.Length == 0,RepairSummary=x.RepairSummary,SlaDeadline=deadline,RemainingHours=remaining,
                    SemaphoreLabel=semaphore.Item1,SemaphoreTone=semaphore.Item2,SemaphoreRank=semaphore.Item3};
            }).OrderBy(x=>x.SemaphoreRank).ThenBy(x=>x.RemainingHours).ThenBy(x=>x.EventAt).ThenBy(x=>x.Id).ToList();
    }
}
