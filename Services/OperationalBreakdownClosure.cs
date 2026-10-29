using System.Security.Claims;
using System.Text.Json;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Security;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Services;
public static class OperationalBreakdownClosure
{
    public static bool CanClose(ClaimsPrincipal user) => user.Identity?.IsAuthenticated==true &&
        (OperationalFleetBoard.CanManage(user) || UserAccessProfiles.Resolve(user) is UserAccessProfiles.MaintenanceEditor or UserAccessProfiles.PurchasingEditor or UserAccessProfiles.InventoryMaintenance);
    public sealed record Result(string? Error, bool Activated);
    public static async Task<Result> CloseAsync(ApplicationDbContext db, SeguimientoToneladasViewModel source, int id, DateTime closedAt, string actor, DateTime nowUtc, CancellationToken cancellationToken=default)
    {
        var record=await db.OperationalBreakdowns.FindAsync([id],cancellationToken);
        if(record==null) return new("No se encontró la avería.",false);
        if(!MaintenanceBreakdownBoard.IsOperationallyOpen(record)) return new("Esta avería ya está cerrada o anulada. No se modificó el equipo.",false);
        closedAt=DateTime.SpecifyKind(closedAt,DateTimeKind.Unspecified);
        if(closedAt<record.EventAt) return new("La fecha de cierre no puede ser anterior al inicio de la avería.",false);
        if(closedAt>DateTime.SpecifyKind(nowUtc.AddHours(-6),DateTimeKind.Unspecified)) return new("La fecha de cierre no puede estar en el futuro. Usa la hora de Nicaragua.",false);
        var masters=await db.OperationalMasterRecords.AsNoTracking().Where(x=>x.Kind=="equipos").ToListAsync(cancellationToken);
        var identity=HolcimMasterCatalog.Identifier(record.Equipment);
        var matches=masters.Where(x=>HolcimMasterCatalog.Identifier(x.Name)==identity || (!string.IsNullOrWhiteSpace(x.Plate) && HolcimMasterCatalog.Identifier(x.Plate)==identity)).ToList();
        if(matches.Count>1) return new("El equipo coincide con varios maestros. Revisa su código antes de activar.",false);
        if(matches.Count==1 && !matches[0].IsActive) return new("El equipo está desactivado en el maestro. Un administrador debe revisarlo antes del cierre y activación.",false);
        var aliases=new HashSet<string>{identity};
        if(matches.Count==1){aliases.Add(HolcimMasterCatalog.Identifier(matches[0].Name));if(!string.IsNullOrWhiteSpace(matches[0].Plate))aliases.Add(HolcimMasterCatalog.Identifier(matches[0].Plate!));}
        var otherRecords=await db.OperationalBreakdowns.AsNoTracking().Where(x=>x.Id!=id && x.EndAt==null).ToListAsync(cancellationToken);
        var hasOtherOpen=otherRecords.Any(x=>aliases.Contains(HolcimMasterCatalog.Identifier(x.Equipment)) && MaintenanceBreakdownBoard.IsOperationallyOpen(x));
        var states=await db.OperationalFleetStates.ToListAsync(cancellationToken);
        if(!hasOtherOpen)
        {
            var key=matches.Count==1 ? "master:"+matches[0].Id : "unit:"+identity;
            var row=states.SingleOrDefault(x=>x.EquipmentKey==key);
            foreach(var alias in states.Where(x=>x.EquipmentKey!=key && aliases.Contains(HolcimMasterCatalog.Identifier(x.EquipmentCode)))) db.OperationalFleetStates.Remove(alias);
            if(row==null){row=new OperationalFleetState{EquipmentKey=key};db.OperationalFleetStates.Add(row);}
            row.EquipmentCode=matches.Count==1?matches[0].Name:record.Equipment;
            row.Status="finalizado";row.Reason=null;row.SourceUploadedAt=source.LatestUploadAt;row.UpdatedAtUtc=nowUtc;
            row.ChangedBy=actor.Length>150?actor[..150]:actor;
        }
        Dictionary<string,string> metadata;
        try {metadata=JsonSerializer.Deserialize<Dictionary<string,string>>(record.SourceJson ?? "{}") ?? [];}
        catch(JsonException){metadata=new(){["Metadatos anteriores"]=record.SourceJson ?? ""};}
        metadata["Cerrado en sistema por"]=actor;
        metadata["Fecha cierre en sistema"]=closedAt.ToString("yyyy-MM-dd HH:mm:ss");
        record.SourceJson=JsonSerializer.Serialize(metadata);
        record.Status="Cerrada";record.EndAt=closedAt;
        await db.SaveChangesAsync(cancellationToken);
        return new(null,!hasOtherOpen);
    }
}
