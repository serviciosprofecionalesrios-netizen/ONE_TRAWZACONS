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
        if(!string.IsNullOrEmpty(search)) query = query.Where(x => x.Equipment.Contains(search) ||
            (x.Driver != null && x.Driver.Contains(search)) || x.Category.Contains(search) || x.Element.Contains(search) ||
            x.Damages.Any(d => d.Category.Contains(search) || d.Element.Contains(search)));
        var total = await query.CountAsync(cancellationToken);
        page = Math.Clamp(page,1,Math.Max(1,(total+49)/50));
        return View(new BreakdownListViewModel { Search=search,Page=page,Total=total,CanManage=OperationalFleetBoard.CanManage(User),
            Records=await query.Include(x => x.Damages).OrderByDescending(x=>x.EventAt).ThenByDescending(x=>x.Id).Skip((page-1)*50).Take(50).ToListAsync(cancellationToken) });
    }
    [HttpGet]
    public async Task<IActionResult> AveriaDetalle(int id, CancellationToken cancellationToken)
    {
        var record = await _context.OperationalBreakdowns.AsNoTracking().Include(x => x.Damages).Include(x => x.MaintenanceTicket)
            .SingleOrDefaultAsync(x=>x.Id==id,cancellationToken);
        if (record == null) return NotFound();
        var ids = record.GetTechnicianIds();
        var technicians = await _context.MaintenanceTechnicians.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        ViewData["BreakdownTechnicians"] = string.Join(", ", ids.Select(id => technicians.GetValueOrDefault(id, $"Técnico #{id}")));
        return View(record);
    }
    private async Task<BreakdownCreateViewModel> BreakdownForm(BreakdownCreateViewModel model, CancellationToken cancellationToken)
    {
        var catalog=await _context.OperationalMasterRecords.AsNoTracking().Where(x=>x.IsActive).ToListAsync(cancellationToken);
        model.EquipmentOptions=catalog.Where(x=>x.Kind=="equipos").Select(x=>x.Name).OrderBy(x=>x).ToList();
        model.DriverOptions=catalog.Where(x=>x.Kind=="conductores").Select(x=>x.Name).OrderBy(x=>x).ToList();
        model.Technicians = await _context.MaintenanceTechnicians.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.FullName).ToListAsync(cancellationToken);
        if (model.Damages.Count == 0) model.Damages.Add(new());
        return model;
    }
    [HttpGet]
    public async Task<IActionResult> NuevaAveria(string? key, CancellationToken cancellationToken)
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
        return View(await BreakdownForm(model, cancellationToken));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevaAveria(BreakdownCreateViewModel model, CancellationToken cancellationToken)
    {
        if(!OperationalFleetBoard.CanManage(User)) return Forbid();
        if(!OperationalBreakdownCatalog.Options("Estatus Equipo").Contains(model.EquipmentStatus)) ModelState.AddModelError(nameof(model.EquipmentStatus),"Selecciona un estatus válido.");
        var catalog = OperationalBreakdownCatalog.Elements;
        var targets = OperationalBreakdownCatalog.Options("DAÑO EN EQUIPO");
        if (model.Damages.Count == 0)
            ModelState.AddModelError(nameof(model.Damages), "Agrega al menos un daño.");
        for (var index = 0; index < model.Damages.Count; index++)
        {
            var damage = model.Damages[index];
            if (damage.Category is null || !catalog.TryGetValue(damage.Category, out var elements) || !elements.Contains(damage.Element))
                ModelState.AddModelError(nameof(model.Damages), $"Daño {index + 1}: selecciona una categoría y un elemento relacionados.");
            if (!targets.Contains(damage.Target))
                ModelState.AddModelError(nameof(model.Damages), $"Daño {index + 1}: selecciona equipo o góndola.");
        }
        var technicianIds = model.TechnicianIds.Distinct().ToArray();
        if (technicianIds.Length > 0 && await _context.MaintenanceTechnicians.CountAsync(
            x => x.IsActive && technicianIds.Contains(x.Id), cancellationToken) != technicianIds.Length)
            ModelState.AddModelError(nameof(model.TechnicianIds), "Selecciona técnicos activos del catálogo.");
        var fleet=BuildFleet(_operacionesSeguimientoStore.Get() ?? new SeguimientoToneladasViewModel());
        if(!fleet.Equipment.Any(x=>x.Code==model.Equipment)) ModelState.AddModelError(nameof(model.Equipment),"Selecciona un equipo del catálogo o del seguimiento actual.");
        if(!ModelState.IsValid) return View(await BreakdownForm(model, cancellationToken));
        var actor=User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "";
        var damages = model.Damages.Select(x => (x.Category, x.Element, x.Target)).Distinct()
            .Select(x => new OperationalBreakdownDamage { Category = x.Category!, Element = x.Element!, Target = x.Target! }).ToList();
        var first = damages[0];
        var record=new OperationalBreakdown { EventAt=DateTime.SpecifyKind(model.EventAt!.Value,DateTimeKind.Unspecified),Equipment=model.Equipment.Trim(),Driver=model.Driver?.Trim(),Circuit=model.Circuit.Trim(),
            EquipmentStatus=model.EquipmentStatus,Category=first.Category,Element=first.Element,DamageTarget=first.Target,
            Damages=damages,EquipmentUnderRepair=model.EquipmentUnderRepair,GondolaUnderRepair=model.GondolaUnderRepair,
            Observation=model.Observation?.Trim(),RegisteredBy=actor.Length>150?actor[..150]:actor };
        record.SetTechnicians(technicianIds);
        static string Limit(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

        var site = record.Circuit.Equals("Limón", StringComparison.OrdinalIgnoreCase) ? "Mina El Limón" : record.Circuit;
        var requester = User.Identity?.Name;
        var description = $"Avería reportada el {record.EventAt:dd/MM/yyyy HH:mm}\n" +
            $"Circuito: {record.Circuit}\nConductor: {record.Driver ?? "Sin registrar"}\n" +
            $"Estatus del equipo: {record.EquipmentStatus}\n" +
            string.Join("\n", damages.Select(d => $"{d.Target} · {d.Category} · {d.Element}"));

        record.MaintenanceTicket = new Ticket
        {
            Department = "Mantenimiento",
            IncidentType = "Mantenimiento Correctivo",
            Status = TicketStatus.Open,
            Priority = PriorityLevel.Medium,
            MaintenanceStage = "Recibida",
            RequestingUser = Limit(string.IsNullOrWhiteSpace(requester) ? actor : requester, 100),
            Site = TicketsController.MaintenanceSites.FirstOrDefault(s => s.Equals(site, StringComparison.OrdinalIgnoreCase)) ?? "",
            UnitCode = Limit(record.Equipment, 80),
            FailureCategory = TicketsController.MaintenanceFailureCategories.FirstOrDefault(c => c.Equals(first.Category, StringComparison.OrdinalIgnoreCase)),
            DamagedElement = Limit(first.Element, 150),
            Description = Limit(description, 2000),
            Observations = record.Observation is null ? null : Limit(record.Observation, 2000),
            SLADeadline = TicketsController.CalculateSLA(PriorityLevel.Medium)
        };

        _context.OperationalBreakdowns.Add(record);
        for (var attempt = 1; ; attempt++)
        {
            record.MaintenanceTicket.TicketNumber = await TicketsController.GenerateTicketNumberAsync(_context, cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException ex) when (attempt < 3 && IsTicketNumberConflict(ex))
            {
                // Another order used this number. Both inserts have been rolled back by EF.
            }
            catch (DbUpdateException ex)
            {
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "No se pudo guardar la avería con su orden de mantenimiento.");
                ModelState.AddModelError(string.Empty, "No se pudo guardar la avería y su orden. Intenta de nuevo.");
                return View(await BreakdownForm(model, cancellationToken));
            }
        }
        TempData["AveriasOk"] = $"Avería registrada. Se creó la orden {record.MaintenanceTicket.TicketNumber}.";
        return RedirectToAction(nameof(AveriaDetalle),new{id=record.Id});
    }

    private static bool IsTicketNumberConflict(DbUpdateException exception) => exception.InnerException switch
    {
        Npgsql.PostgresException pg => pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation &&
            pg.ConstraintName == "IX_Tickets_TicketNumber",
        Microsoft.Data.SqlClient.SqlException sql => (sql.Number is 2601 or 2627) &&
            sql.Message.Contains("IX_Tickets_TicketNumber", StringComparison.Ordinal),
        _ => false
    };
}
