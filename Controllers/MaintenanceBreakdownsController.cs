using System.ComponentModel.DataAnnotations;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Controllers;
[Authorize]
public sealed class MaintenanceBreakdownsController(ApplicationDbContext db, OperacionesSeguimientoStore store) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Close(int id, bool returnToBoard=false)
    {
        if(!OperationalBreakdownClosure.CanClose(User)) return Forbid();
        var record=await db.OperationalBreakdowns.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id);
        if(record==null)return NotFound();
        return View(new CloseForm{Id=id,Record=record,ReturnToBoard=returnToBoard,ClosedAt=DateTime.UtcNow.AddHours(-6)});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(CloseForm model, CancellationToken cancellationToken)
    {
        if(!OperationalBreakdownClosure.CanClose(User)) return Forbid();
        var record=await db.OperationalBreakdowns.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==model.Id,cancellationToken);
        if(record==null)return NotFound();
        model.Record=record;
        if(!ModelState.IsValid)return View(model);
        var actor=User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "";
        var result=await OperationalBreakdownClosure.CloseAsync(db,store.Get() ?? new SeguimientoToneladasViewModel(),model.Id,model.ClosedAt!.Value,actor,DateTime.UtcNow,cancellationToken);
        if(result.Error!=null){ModelState.AddModelError("",result.Error);return View(model);}
        TempData["ClosureOk"]=result.Activated?"Incidencia cerrada. El equipo quedó activo en Equipos descargados.":"Incidencia cerrada. El equipo conserva otra avería abierta; se activará al cerrar la última.";
        return RedirectToAction(nameof(Close),new{id=model.Id,returnToBoard=model.ReturnToBoard});
    }
    public sealed class CloseForm
    {
        public int Id {get;set;}
        [Required(ErrorMessage="Indica la fecha y hora de cierre.")] public DateTime? ClosedAt {get;set;}
        public bool ReturnToBoard {get;set;}
        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever] public OperationalBreakdown? Record {get;set;}
    }
}
