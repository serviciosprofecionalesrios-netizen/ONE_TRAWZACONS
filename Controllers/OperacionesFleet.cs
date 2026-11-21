using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
namespace ITServiceDeskApp.Controllers;
public partial class OperacionesController
{
    [HttpGet]
    public IActionResult ExportarImcMensual(string? imcMonth)
    {
        var month = SelectImcMonth(imcMonth, UploadedImcMonths());
        if (month is null || (imcMonth is not null && imcMonth != month)) return BadRequest();

        var adjustments = _context.OperationalImcClearances.AsNoTracking()
            .Where(row => row.Month == month).ToDictionary(row => row.UnitKey);
        var inspections = ImcHistory.InspectionsForMonth(month, UploadedImcInspections(month))
            .OrderByDescending(row => row.PresentedAt).ThenBy(row => row.Unit, StringComparer.Ordinal).ToList();
        var groups = inspections.ToLookup(row => HolcimMasterCatalog.Identifier(row.Unit));
        var colors = new[] { "Rojo", "Amarillo", "Verde" };

        ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var summary = package.Workbook.Worksheets.Add("Resumen");
        var detail = package.Workbook.Worksheets.Add("Unidad y mes");
        var observations = package.Workbook.Worksheets.Add("Inspecciones");
        var headers = new List<string> { "Mes", "Unidad", "Primera inspección", "Última inspección", "Último color observado",
            "Color corregido", "Color del panel", "Inspecciones", "Días observados", "Permanencia" };
        foreach (var color in colors)
            headers.AddRange([$"Primera fecha {color}", $"Última fecha {color}", $"Días con {color}"]);
        detail.Cells[1, 1].LoadFromArrays([headers.ToArray()]);
        var effectiveCounts = colors.ToDictionary(color => color, _ => 0);
        var rowIndex = 2;
        foreach (var key in groups.Select(group => group.Key).Union(adjustments.Keys).Order(StringComparer.Ordinal))
        {
            var rows = groups[key].OrderByDescending(row => row.PresentedAt).ToList();
            var latest = rows.FirstOrDefault();
            var adjustment = adjustments.GetValueOrDefault(key);
            var effectiveColor = adjustment?.Color ?? latest!.Color;
            effectiveCounts[effectiveColor]++;
            var values = new List<object?> { month, adjustment?.UnitCode ?? latest!.Unit, rows.LastOrDefault()?.PresentedAt,
                latest?.PresentedAt, latest?.Color, adjustment?.Color, effectiveColor, rows.Count,
                rows.Select(row => row.PresentedAt.Date).Distinct().Count(), "No determinable" };
            foreach (var color in colors)
            {
                var dates = rows.Where(row => row.Color == color).Select(row => row.PresentedAt.Date).Distinct().ToList();
                values.AddRange([dates.Count == 0 ? null : dates[^1], dates.Count == 0 ? null : dates[0], dates.Count]);
            }
            detail.Cells[rowIndex++, 1].LoadFromArrays([values.ToArray()]);
        }
        detail.Cells[2, 3, rowIndex - 1, 4].Style.Numberformat.Format = "dd/mm/yyyy hh:mm:ss";
        for (var column = 11; column <= 17; column += 3)
            detail.Cells[2, column, rowIndex - 1, column + 1].Style.Numberformat.Format = "dd/mm/yyyy";
        observations.Cells[1, 1].LoadFromArrays(new[] { new object[] { "Unidad", "Presentada", "Semáforo" } }
            .Concat(inspections.Select(row => new object[] { row.Unit, row.PresentedAt, row.Color })));
        observations.Cells[2, 2, inspections.Count + 1, 2].Style.Numberformat.Format = "dd/mm/yyyy hh:mm:ss";
        summary.Cells[1, 1].LoadFromArrays([
            ["Indicador", "Valor"], ["Mes", month], ["Unidades", rowIndex - 2], ["Inspecciones", inspections.Count],
            ["Unidades rojas", effectiveCounts["Rojo"]], ["Unidades amarillas", effectiveCounts["Amarillo"]],
            ["Unidades verdes", effectiveCounts["Verde"]],
            ["Días por color", "Fechas distintas con inspección del color. Un día puede tener varios colores."],
            ["Permanencia", "No determinable entre inspecciones."],
            ["Color corregido", "Ajuste manual del panel. El registro de ajustes no incluye fecha."],
            ["Fuente", "Historial IMC cargado en el panel."]
        ]);
        foreach (var sheet in package.Workbook.Worksheets)
        {
            sheet.Cells[1, 1, 1, sheet.Dimension.End.Column].Style.Font.Bold = true;
            sheet.View.FreezePanes(2, 1);
            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }
        detail.Cells[detail.Dimension.Address].AutoFilter = true;
        observations.Cells[observations.Dimension.Address].AutoFilter = true;
        return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"IMC-{month}.xlsx");
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> ImportarInspeccionesImc(IFormFile? imcFile, string? imcMonth, CancellationToken cancellationToken)
    {
        if (!OperationalFleetBoard.CanManage(User)) return Forbid();

        IActionResult Back(string? month) =>
            RedirectToAction(nameof(SeguimientoToneladas), null, new { imcMonth = month }, "imc-history");

        if (imcFile is null || imcFile.Length == 0 || imcFile.Length > ImcInspectionWorkbook.MaxFileBytes ||
            !Path.GetExtension(imcFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ImcImportError"] = "Selecciona un archivo Excel .xlsx de hasta 10 MB.";
            return Back(imcMonth);
        }

        List<OperationalImcInspection> rows;
        try
        {
            using var buffer = new MemoryStream();
            await imcFile.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            rows = ImcInspectionWorkbook.Read(buffer);
        }
        catch (InvalidDataException ex)
        {
            TempData["ImcImportError"] = ex.Message;
            return Back(imcMonth);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or FormatException
                                   or System.Xml.XmlException or NotSupportedException)
        {
            TempData["ImcImportError"] = "No se pudo leer el Excel. Revisa que abra correctamente y vuelve a subirlo.";
            return Back(imcMonth);
        }

        var months = rows.Select(row => row.Month).Distinct().ToArray();
        var bundledKeys = months.SelectMany(month => ImcHistory.InspectionsForMonth(month))
            .Select(row => (HolcimMasterCatalog.Identifier(row.Unit), row.PresentedAt, row.Color)).ToArray();
        for (var attempt = 1; ; attempt++)
        {
            var saved = await _context.OperationalImcInspections.AsNoTracking().Where(row => months.Contains(row.Month))
                .Select(row => new { row.UnitKey, row.PresentedAt, row.Color }).ToListAsync(cancellationToken);
            var keys = saved.Select(row => (row.UnitKey, row.PresentedAt, row.Color)).Concat(bundledKeys).ToHashSet();
            var pending = rows.Where(row => keys.Add((row.UnitKey, row.PresentedAt, row.Color))).ToList();
            try
            {
                _context.OperationalImcInspections.AddRange(pending);
                await _context.SaveChangesAsync(cancellationToken);
                TempData["ImcImportOk"] = $"{pending.Count:N0} inspecciones importadas · {rows.Count - pending.Count:N0} duplicadas omitidas.";
                return Back(months.Max(StringComparer.Ordinal));
            }
            catch (DbUpdateException ex) when (attempt < 3 && IsImcInspectionKeyConflict(ex))
            {
                foreach (var row in pending) _context.Entry(row).State = EntityState.Detached;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "No se pudieron guardar las inspecciones IMC.");
                TempData["ImcImportError"] = "No se pudo guardar la carga de inspecciones. Inténtalo de nuevo.";
                return Back(imcMonth);
            }
        }
    }

    private static bool IsImcInspectionKeyConflict(DbUpdateException exception) => exception.InnerException switch
    {
        Npgsql.PostgresException pg => pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation &&
            pg.ConstraintName == "PK_OperationalImcInspections",
        Microsoft.Data.SqlClient.SqlException sql => (sql.Number is 2601 or 2627) &&
            sql.Message.Contains("PK_OperationalImcInspections", StringComparison.Ordinal),
        _ => false
    };

    private string[] UploadedImcMonths() =>
        _context.OperationalImcInspections.Select(row => row.Month).Distinct().ToArray();

    private List<OperationalImcInspection> UploadedImcInspections(string? month) =>
        _context.OperationalImcInspections.AsNoTracking().Where(row => row.Month == month).ToList();

    private static string? SelectImcMonth(string? requested, IEnumerable<string> uploadedMonths)
    {
        var months = ImcHistory.Months(uploadedMonths);
        return requested is not null && months.Contains(requested) ? requested : months.FirstOrDefault();
    }

    private ViewResult SeguimientoFleetView(SeguimientoToneladasViewModel model, string? imcMonth = null)
    {
        var fleetSource = model.IsFromUpload ? model : _operacionesSeguimientoStore.Get() ?? model;
        model.Fleet = BuildFleet(fleetSource);
        ApplyImcMonth(model.Fleet, imcMonth);
        return View("SeguimientoToneladas",model);
    }

    private void ApplyImcMonth(FleetBoardViewModel board, string? imcMonth)
    {
        var uploadedMonths = UploadedImcMonths();
        var selectedMonth = SelectImcMonth(imcMonth, uploadedMonths);
        var clearances = _context.OperationalImcClearances.AsNoTracking()
            .Where(row => row.Month == selectedMonth).ToList();
        board.Imc = ImcHistory.ForMonth(selectedMonth, clearances, UploadedImcInspections(selectedMonth), uploadedMonths);
        var imcColors = board.Imc.Cards
            .SelectMany(card => card.Units.Select(unit => (Unit: HolcimMasterCatalog.Identifier(unit), card.ColorClass)))
            .ToDictionary(entry => entry.Unit, entry => entry.ColorClass);
        foreach (var equipment in board.Equipment)
        {
            equipment.ImcMonth = selectedMonth;
            equipment.ImcColor = imcColors.GetValueOrDefault(HolcimMasterCatalog.Identifier(equipment.Code));
        }
    }
    private FleetBoardViewModel BuildFleet(SeguimientoToneladasViewModel source)
    {
        var catalog = _context.OperationalMasterRecords.AsNoTracking().ToList();
        var board = OperationalFleetBoard.Build(catalog,source,_context.OperationalFleetStates.AsNoTracking().ToList(),_context.OperationalFleetDrivers.AsNoTracking().ToList(),
            _context.OperationalBreakdowns.AsNoTracking().ToList());
        board.CanManage = OperationalFleetBoard.CanManage(User);
        var drivers = catalog.Where(x => x.Kind == "conductores" && x.IsActive).OrderBy(x => x.Name).ToList();
        foreach(var equipment in board.Equipment) { equipment.CanManage = board.CanManage; equipment.CanChangeStatus = OperationalFleetBoard.CanChangeStatus(User); if(board.CanManage) equipment.AvailableDrivers = drivers; }
        return board;
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarConductorEquipo(string key, int driverId, string loadVersion, CancellationToken cancellationToken, bool desasignar = false)
    {
        if (!OperationalFleetBoard.CanManage(User)) return Forbid();
        var board = BuildFleet(_operacionesSeguimientoStore.Get() ?? new SeguimientoToneladasViewModel());
        if (board.LoadVersion != loadVersion)
        {
            TempData["FleetError"] = "Se cargó otro archivo de TiHouse. Revisa el equipo y vuelve a intentar.";
            return RedirectToAction(nameof(SeguimientoToneladas),null,null,"fleet-board");
        }
        var equipment = board.Equipment.SingleOrDefault(x => x.Key == key);
        var driver = desasignar ? null : await _context.OperationalMasterRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == driverId && x.Kind == "conductores" && x.IsActive,cancellationToken);
        if (equipment == null || (!desasignar && driver == null)) return BadRequest();
        var assignment = await _context.OperationalFleetDrivers.FindAsync([key],cancellationToken);
        if (assignment == null) { assignment = new OperationalFleetDriver { EquipmentKey = key }; _context.OperationalFleetDrivers.Add(assignment); }
        assignment.EquipmentCode = equipment.Code;
        // Zero records an explicit removal, preventing TiHouse from restoring it.
        assignment.DriverId = desasignar ? 0 : driver!.Id;
        assignment.UpdatedAtUtc = DateTime.UtcNow;
        var actor = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "";
        assignment.ChangedBy = actor.Length > 150 ? actor[..150] : actor;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["FleetOk"] = desasignar ? $"Conductor desasignado de {equipment.Code}." : $"Conductor {driver!.Name} asignado a {equipment.Code}.";
        return RedirectToAction(nameof(SeguimientoToneladas),null,null,"fleet-board");
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarEstadoEquipo(string key, string status, string? reason, string loadVersion,
        CancellationToken cancellationToken, string? imcMonth = null)
    {
        if (!OperationalFleetBoard.CanChangeStatus(User)) return Forbid();
        var source = _operacionesSeguimientoStore.Get() ?? new SeguimientoToneladasViewModel();
        var board = BuildFleet(source);
        if (board.LoadVersion != loadVersion)
        {
            TempData["FleetError"] = "Se cargó otro archivo de TiHouse. Revisa la distribución actual y vuelve a intentar.";
            return RedirectToAction(nameof(SeguimientoToneladas),null,null,"fleet-board");
        }
        var equipment = board.Equipment.SingleOrDefault(x => x.Key == key);
        if (equipment == null) return BadRequest();
        if (status == "imc-verde" || (imcMonth is not null && status is ("imc-roja" or "imc-amarilla")))
        {
            if (string.IsNullOrEmpty(imcMonth) || imcMonth != SelectImcMonth(imcMonth, UploadedImcMonths()))
                return BadRequest();
            var color = status switch { "imc-roja" => "Rojo", "imc-amarilla" => "Amarillo", _ => "Verde" };
            var unitKey = HolcimMasterCatalog.Identifier(equipment.Code);
            var adjustments = _context.OperationalImcClearances;
            var adjustment = await adjustments.FindAsync([imcMonth, unitKey], cancellationToken);
            var isNew = adjustment is null;
            if (adjustment is null)
            {
                adjustment = new OperationalImcClearance { Month = imcMonth, UnitKey = unitKey, UnitCode = equipment.Code };
                adjustments.Add(adjustment);
            }
            adjustment.Color = color;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (isNew)
            {
                // A concurrent request may have created the adjustment first.
                _context.Entry(adjustment).State = EntityState.Detached;
                var existing = await adjustments.FindAsync([imcMonth, unitKey], cancellationToken);
                if (existing is null) throw;
                existing.Color = color;
                await _context.SaveChangesAsync(cancellationToken);
            }
            TempData["FleetOk"] = $"IMC de {equipment.Code} marcado en {color.ToLowerInvariant()}.";
            return RedirectToAction(nameof(SeguimientoToneladas), null, new { imcMonth }, "fleet-board");
        }
        if (!OperationalFleetBoard.ManualStatuses.Contains(status) ||
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
        return RedirectToAction(nameof(SeguimientoToneladas),null,new { imcMonth },"fleet-board");
    }
}
