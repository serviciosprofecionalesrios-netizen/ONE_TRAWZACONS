using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Controllers;
public partial class OperacionesController
{
    [HttpGet]
    public async Task<IActionResult> Averias(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = _context.OperationalBreakdowns.AsNoTracking();
        search = search?.Trim();
        if(!string.IsNullOrEmpty(search)) query = query.Where(x => x.Equipment.Contains(search) || (x.Driver != null && x.Driver.Contains(search)) || x.Category.Contains(search) || x.Element.Contains(search));
        var total = await query.CountAsync(cancellationToken);
        page = Math.Clamp(page,1,Math.Max(1,(total+49)/50));
        return View(new BreakdownListViewModel { Search=search,Page=page,Total=total,CanManage=OperationalFleetBoard.CanManage(User),
            Records=await query.OrderByDescending(x=>x.EventAt).ThenByDescending(x=>x.Id).Skip((page-1)*50).Take(50).ToListAsync(cancellationToken) });
    }
    [HttpGet]
    public async Task<IActionResult> AveriaDetalle(int id, CancellationToken cancellationToken)
    {
        var record = await _context.OperationalBreakdowns.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,cancellationToken);
        return record == null ? NotFound() : View(record);
    }
    private BreakdownCreateViewModel BreakdownForm(BreakdownCreateViewModel model)
    {
        var catalog=_context.OperationalMasterRecords.AsNoTracking().Where(x=>x.IsActive).ToList();
        model.EquipmentOptions=catalog.Where(x=>x.Kind=="equipos").Select(x=>x.Name).OrderBy(x=>x).ToList();
        model.DriverOptions=catalog.Where(x=>x.Kind=="conductores").Select(x=>x.Name).OrderBy(x=>x).ToList();
        return model;
    }
    [HttpGet]
    public IActionResult NuevaAveria(string? key)
    {
        if(!OperationalFleetBoard.CanManage(User)) return Forbid();
        var model=new BreakdownCreateViewModel();
        if(!string.IsNullOrWhiteSpace(key))
        {
            var fleet=BuildFleet(_operacionesSeguimientoStore.Get() ?? new SeguimientoToneladasViewModel());
            var equipment=fleet.Equipment.SingleOrDefault(x=>x.Key==key);
            if(equipment==null) return BadRequest();
            model.Equipment=equipment.Code;model.Driver=equipment.Driver;
        }
        return View(BreakdownForm(model));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevaAveria(BreakdownCreateViewModel model, CancellationToken cancellationToken)
    {
        if(!OperationalFleetBoard.CanManage(User)) return Forbid();
        if(!OperationalBreakdownCatalog.Options("Estatus Equipo").Contains(model.EquipmentStatus)) ModelState.AddModelError(nameof(model.EquipmentStatus),"Selecciona un estatus válido.");
        if(!OperationalBreakdownCatalog.Elements.TryGetValue(model.Category,out var elements) || !elements.Contains(model.Element)) ModelState.AddModelError(nameof(model.Element),"Selecciona una categoría y un elemento de daño relacionados.");
        if(!OperationalBreakdownCatalog.Options("DAÑO EN EQUIPO").Contains(model.DamageTarget)) ModelState.AddModelError(nameof(model.DamageTarget),"Selecciona equipo o góndola.");
        var fleet=BuildFleet(_operacionesSeguimientoStore.Get() ?? new SeguimientoToneladasViewModel());
        if(!fleet.Equipment.Any(x=>x.Code==model.Equipment)) ModelState.AddModelError(nameof(model.Equipment),"Selecciona un equipo del catálogo o del seguimiento actual.");
        if(!ModelState.IsValid) return View(BreakdownForm(model));
        var actor=User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "";
        var record=new OperationalBreakdown { EventAt=DateTime.SpecifyKind(model.EventAt!.Value,DateTimeKind.Unspecified),Equipment=model.Equipment.Trim(),Driver=model.Driver?.Trim(),Circuit=model.Circuit.Trim(),
            EquipmentStatus=model.EquipmentStatus,Category=model.Category,Element=model.Element,DamageTarget=model.DamageTarget,Observation=model.Observation?.Trim(),RegisteredBy=actor.Length>150?actor[..150]:actor };
        _context.OperationalBreakdowns.Add(record);await _context.SaveChangesAsync(cancellationToken);
        TempData["AveriasOk"]="Avería registrada correctamente.";
        return RedirectToAction(nameof(AveriaDetalle),new{id=record.Id});
    }
}
