using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Controllers;
public partial class OperacionesController
{
    private ViewResult SeguimientoFleetView(SeguimientoToneladasViewModel model)
    {
        var fleetSource = model.IsFromUpload ? model : _operacionesSeguimientoStore.Get() ?? model;
        model.Fleet = BuildFleet(fleetSource);
        return View("SeguimientoToneladas",model);
    }
    private FleetBoardViewModel BuildFleet(SeguimientoToneladasViewModel source)
    {
        var catalog = _context.OperationalMasterRecords.AsNoTracking().ToList();
        var board = OperationalFleetBoard.Build(catalog,source,_context.OperationalFleetStates.AsNoTracking().ToList(),_context.OperationalFleetDrivers.AsNoTracking().ToList());
        board.CanManage = OperationalFleetBoard.CanManage(User);
        var drivers = catalog.Where(x => x.Kind == "conductores" && x.IsActive).OrderBy(x => x.Name).ToList();
        foreach(var equipment in board.Equipment) { equipment.CanManage = board.CanManage; if(board.CanManage) equipment.AvailableDrivers = drivers; }
        return board;
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarConductorEquipo(string key, int driverId, string loadVersion, CancellationToken cancellationToken)
    {
        if (!OperationalFleetBoard.CanManage(User)) return Forbid();
        var board = BuildFleet(_operacionesSeguimientoStore.Get() ?? new SeguimientoToneladasViewModel());
        if (board.LoadVersion != loadVersion)
        {
            TempData["FleetError"] = "Se cargó otro archivo de TiHouse. Revisa el equipo y vuelve a intentar.";
            return RedirectToAction(nameof(SeguimientoToneladas),null,null,"fleet-board");
        }
        var equipment = board.Equipment.SingleOrDefault(x => x.Key == key);
        var driver = await _context.OperationalMasterRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == driverId && x.Kind == "conductores" && x.IsActive,cancellationToken);
        if (equipment == null || driver == null) return BadRequest();
        var assignment = await _context.OperationalFleetDrivers.FindAsync([key],cancellationToken);
        if (assignment == null) { assignment = new OperationalFleetDriver { EquipmentKey = key }; _context.OperationalFleetDrivers.Add(assignment); }
        assignment.EquipmentCode = equipment.Code;
        assignment.DriverId = driver.Id;
        assignment.UpdatedAtUtc = DateTime.UtcNow;
        var actor = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "";
        assignment.ChangedBy = actor.Length > 150 ? actor[..150] : actor;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["FleetOk"] = $"Conductor {driver.Name} asignado a {equipment.Code}.";
        return RedirectToAction(nameof(SeguimientoToneladas),null,null,"fleet-board");
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarEstadoEquipo(string key, string status, string? reason, string loadVersion, CancellationToken cancellationToken)
    {
        if (!OperationalFleetBoard.CanManage(User)) return Forbid();
        var source = _operacionesSeguimientoStore.Get() ?? new SeguimientoToneladasViewModel();
        var board = BuildFleet(source);
        if (board.LoadVersion != loadVersion)
        {
            TempData["FleetError"] = "Se cargó otro archivo de TiHouse. Revisa la distribución actual y vuelve a intentar.";
            return RedirectToAction(nameof(SeguimientoToneladas),null,null,"fleet-board");
        }
        var equipment = board.Equipment.SingleOrDefault(x => x.Key == key);
        if (equipment == null || !OperationalFleetBoard.ManualStatuses.Contains(status) ||
            (status == "fuera-ruta" && !OperationalFleetBoard.Reasons.Contains(reason))) return BadRequest();
        var row = await _context.OperationalFleetStates.FindAsync([equipment.ManualStateKey ?? key],cancellationToken);
        if (status == "auto")
        {
            var keysToClear = equipment.ManualStateKeys.Append(key).Distinct().ToList();
            var classifications = await _context.OperationalFleetStates.Where(x => keysToClear.Contains(x.EquipmentKey)).ToListAsync(cancellationToken);
            _context.OperationalFleetStates.RemoveRange(classifications);
        }
        else
        {
            if (row == null) { row = new OperationalFleetState {EquipmentKey = key}; _context.OperationalFleetStates.Add(row); }
            row.EquipmentCode = equipment.Code;
            row.Status = status; row.Reason = status == "fuera-ruta" ? reason : null;
            row.SourceUploadedAt = source.LatestUploadAt; row.UpdatedAtUtc = DateTime.UtcNow;
            var actor = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "";
            row.ChangedBy = actor.Length > 150 ? actor[..150] : actor;
        }
        await _context.SaveChangesAsync(cancellationToken);
        TempData["FleetOk"] = "Estado del equipo guardado.";
        return RedirectToAction(nameof(SeguimientoToneladas),null,null,"fleet-board");
    }
}
