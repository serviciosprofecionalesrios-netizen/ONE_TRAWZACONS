using System.ComponentModel.DataAnnotations;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Services.Interfaces;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Controllers;
[Authorize]
public sealed class MaintenanceBreakdownsController(ApplicationDbContext db, IOperacionesSeguimientoStore store) : Controller
{
    private async Task<CloseForm> Prepare(CloseForm model)
    {
        var assignedIds = model.Record!.GetTechnicianIds();
        model.Technicians=await db.MaintenanceTechnicians.AsNoTracking()
            .Where(x=>x.IsActive || assignedIds.Contains(x.Id)).OrderBy(x=>x.FullName).ToListAsync();
        return model;
    }
    [HttpGet]
    public async Task<IActionResult> Close(int id, bool returnToBoard=false)
    {
        if(!OperationalBreakdownClosure.CanClose(User)) return Forbid();
        var record=await db.OperationalBreakdowns.AsNoTracking().Include(x => x.Damages).SingleOrDefaultAsync(x=>x.Id==id);
        if(record==null)return NotFound();
        return View(await Prepare(new CloseForm
        {
            Id=id, Record=record, ReturnToBoard=returnToBoard, ClosedAt=DateTime.UtcNow.AddHours(-6),
            TechnicianIds=record.GetTechnicianIds().ToList(), SlaDeadline=MaintenanceBreakdownBoard.Deadline(record),
            EquipmentUnderRepair=record.EquipmentUnderRepair, GondolaUnderRepair=record.GondolaUnderRepair
        }));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(CloseForm model, CancellationToken cancellationToken,string operation="close")
    {
        if(!OperationalBreakdownClosure.CanClose(User)) return Forbid();
        var record=await db.OperationalBreakdowns.Include(x => x.Damages).SingleOrDefaultAsync(x=>x.Id==model.Id,cancellationToken);
        if(record==null)return NotFound();
        model.Record=record;
        if(!MaintenanceBreakdownBoard.IsOperationallyOpen(record))ModelState.AddModelError("","Esta avería ya está cerrada o anulada.");
        if(operation is not "assign" and not "close")return BadRequest();
        if(operation=="close" && !model.ClosedAt.HasValue)ModelState.AddModelError(nameof(model.ClosedAt),"Indica la fecha y hora de cierre.");
        var newTechnicians = model.TechnicianIds.Except(record.GetTechnicianIds()).ToArray();
        if (newTechnicians.Length > 0 && await db.MaintenanceTechnicians.CountAsync(
            x => x.IsActive && newTechnicians.Contains(x.Id), cancellationToken) != newTechnicians.Length)
            ModelState.AddModelError(nameof(model.TechnicianIds), "Selecciona técnicos activos del catálogo.");
        if(model.SlaDeadline.HasValue && model.SlaDeadline<record.EventAt)ModelState.AddModelError(nameof(model.SlaDeadline),"El plazo no puede ser anterior al inicio.");
        if(!ModelState.IsValid)return View(await Prepare(model));
        var actor=User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "";
        record.SetTechnicians(model.TechnicianIds);
        record.SlaDeadline=model.SlaDeadline.HasValue?DateTime.SpecifyKind(model.SlaDeadline.Value,DateTimeKind.Unspecified):null;
        try
        {
            if(operation=="assign")
            {
                record.EquipmentUnderRepair = model.EquipmentUnderRepair;
                record.GondolaUnderRepair = model.GondolaUnderRepair;
                await db.SaveChangesAsync(cancellationToken);
                TempData["ClosureOk"]="Técnicos, plazo y reparación guardados. La avería continúa abierta.";
            }
            else
            {
                var result=await OperationalBreakdownClosure.CloseAsync(db,store.Get() ?? new SeguimientoToneladasViewModel(),model.Id,model.ClosedAt!.Value,actor,DateTime.UtcNow,cancellationToken);
                if(result.Error!=null){ModelState.AddModelError("",result.Error);return View(await Prepare(model));}
                TempData["ClosureOk"]=result.Activated?"Incidencia cerrada. El equipo quedó activo en Equipos descargados.":"Incidencia cerrada. El equipo conserva otra avería abierta; se activará al cerrar la última.";
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ClosureConflict"]="Otra sesión cambió la avería mientras guardabas. Revisa su estado actual.";
        }
        return RedirectToAction(nameof(Close),new{id=model.Id,returnToBoard=model.ReturnToBoard});
    }
    public sealed class CloseForm
    {
        public int Id {get;set;}
        public DateTime? ClosedAt {get;set;}
        public List<int> TechnicianIds { get; set; } = [];
        public bool EquipmentUnderRepair { get; set; }
        public bool GondolaUnderRepair { get; set; }
        public DateTime? SlaDeadline {get;set;}
        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever] public List<MaintenanceTechnician> Technicians {get;set;}=[];
        public bool ReturnToBoard {get;set;}
        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever] public OperationalBreakdown? Record {get;set;}
    }
}
