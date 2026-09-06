using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels.Hs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral,GestorHS,GerenteHS,SupervisorHS")]
    public class HsController : Controller
    {
        private static readonly string[] Sites =
        {
            "Plantel San Benito",
            "Mina El Limon",
            "Mina La Libertad",
            "Oficinas Centrales TW"
        };

        private static readonly string[] IncidentTypes =
        {
            "Accidente",
            "Incidente",
            "Casi accidente",
            "Condicion insegura",
            "Acto inseguro",
            "Ambiental"
        };

        private static readonly string[] Severities = { "Baja", "Media", "Alta", "Critica" };
        private static readonly string[] IncidentStatuses = { "Abierto", "Investigacion", "Accion requerida", "Cerrado" };
        private static readonly string[] ActionStatuses = { "Abierta", "En progreso", "Vencida", "En revision", "Aprobada", "Cerrada", "Rechazada" };
        private static readonly string[] Priorities = { "Baja", "Media", "Alta", "Critica" };
        private static readonly string[] PermitStatuses = { "Solicitado", "Aprobado", "En ejecucion", "Cerrado", "Rechazado", "Solicita informacion extra" };
        private static readonly string[] PermitTypes = { "Trabajo en caliente", "Trabajo en altura", "Espacio confinado", "Izaje de carga", "Excavacion", "Electrico", "Bloqueo/Etiquetado", "Energia peligrosa" };
        private static readonly string[] DocumentTypes = { "Politica", "Procedimiento", "Matriz IPER", "AST/JSA", "Permiso", "Acta", "Reporte", "Auditoria", "Simulacro" };
        private static readonly string[] DocumentStatuses = { "Borrador", "Vigente", "En revision", "Obsoleto" };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public HsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard(DateTime? startDate = null, DateTime? endDate = null)
        {
            var today = DateTime.Today;
            var start = startDate?.Date ?? new DateTime(today.Year, today.Month, 1);
            var end = endDate?.Date ?? today;
            if (end < start)
            {
                (start, end) = (end, start);
            }

            var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var endExclusiveUtc = DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Utc);
            var nowUtc = DateTime.UtcNow;

            await RefreshOverdueActionsAsync(nowUtc);

            var incidents = await _context.HsIncidents
                .AsNoTracking()
                .Where(x => x.OccurredAtUtc >= startUtc && x.OccurredAtUtc < endExclusiveUtc)
                .OrderByDescending(x => x.OccurredAtUtc)
                .ToListAsync();
            var inspections = await _context.HsInspections
                .AsNoTracking()
                .Where(x => x.InspectionDateUtc >= startUtc && x.InspectionDateUtc < endExclusiveUtc)
                .OrderByDescending(x => x.InspectionDateUtc)
                .ToListAsync();
            var trainings = await _context.HsTrainingRecords
                .AsNoTracking()
                .Where(x => x.TrainingDateUtc >= startUtc && x.TrainingDateUtc < endExclusiveUtc)
                .ToListAsync();
            var actions = await _context.HsCorrectiveActions
                .AsNoTracking()
                .OrderBy(x => x.DueDateUtc)
                .ToListAsync();
            var permits = await _context.HsWorkPermits
                .AsNoTracking()
                .Where(x => x.StartAtUtc < endExclusiveUtc && x.EndAtUtc >= startUtc)
                .ToListAsync();
            var documents = await _context.HsDocuments
                .AsNoTracking()
                .ToListAsync();

            var siteSummaries = incidents
                .GroupBy(x => x.Site, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var siteInspections = inspections
                        .Where(x => x.Site.Equals(g.Key, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    return new HsSiteSummaryViewModel
                    {
                        Site = g.Key,
                        Incidents = g.Count(),
                        OpenActions = actions.Count(x => x.Status != "Cerrada" && x.Description.Contains(g.Key, StringComparison.OrdinalIgnoreCase)),
                        AverageScore = siteInspections.Count == 0 ? 0m : Math.Round(siteInspections.Average(x => x.ScorePercent), 2)
                    };
                })
                .OrderByDescending(x => x.Incidents)
                .ThenBy(x => x.Site)
                .ToList();

            var truckInspectionSummaries = inspections
                .Where(x =>
                    x.Category == "Croquis camion" &&
                    !string.IsNullOrWhiteSpace(x.EquipmentCode))
                .GroupBy(x => x.EquipmentCode!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var last = g.OrderByDescending(x => x.InspectionDateUtc).First();
                    return new HsTruckInspectionSummaryViewModel
                    {
                        EquipmentCode = g.Key,
                        Total = g.Count(),
                        LastInspectionAt = last.InspectionDateUtc,
                        LastScorePercent = last.ScorePercent,
                        OpenFindings = g.Sum(x => x.FindingsCount)
                    };
                })
                .OrderByDescending(x => x.LastInspectionAt)
                .ThenBy(x => x.EquipmentCode)
                .Take(12)
                .ToList();

            var pendingPermitApprovals = permits.Count(IsPermitWaitingForApprovalResponse);
            var overduePermitApprovals = permits.Count(x =>
                IsPermitWaitingForApprovalResponse(x) &&
                x.ApprovalResponseDueAtUtc.HasValue &&
                x.ApprovalResponseDueAtUtc.Value < nowUtc);

            var model = new HsDashboardViewModel
            {
                StartDate = start,
                EndDate = end,
                OpenIncidents = incidents.Count(x => x.Status != "Cerrado"),
                CriticalIncidents = incidents.Count(x => x.Severity == "Critica"),
                LostTimeIncidents = incidents.Count(x => x.LostTime),
                OpenActions = actions.Count(x => x.Status != "Cerrada"),
                OverdueActions = actions.Count(x => x.Status != "Cerrada" && x.DueDateUtc.Date < nowUtc.Date),
                Inspections = inspections.Count,
                AverageInspectionScore = inspections.Count == 0 ? 0m : Math.Round(inspections.Average(x => x.ScorePercent), 2),
                Trainings = trainings.Count,
                TrainedPeople = trainings.Sum(x => x.AttendeeCount),
                ActivePermits = permits.Count(x => x.Status == "Aprobado" || x.Status == "En ejecucion"),
                PendingPermitApprovals = pendingPermitApprovals,
                OverduePermitApprovals = overduePermitApprovals,
                HighRiskIncidents = incidents.Count(x => x.RiskScore >= 15 || x.ResidualRiskScore >= 15),
                PendingInvestigations = incidents.Count(x => x.Status != "Cerrado" && (!x.InvestigationClosedAtUtc.HasValue || string.IsNullOrWhiteSpace(x.RootCause))),
                PendingApprovals = actions.Count(x => x.Status == "En revision") + pendingPermitApprovals,
                ExpiredTrainings = trainings.Count(x => x.ExpirationDateUtc.HasValue && x.ExpirationDateUtc.Value.Date < nowUtc.Date),
                DocumentsDueForReview = documents.Count(x => x.Status == "Vigente" && x.ReviewDateUtc.HasValue && x.ReviewDateUtc.Value.Date <= nowUtc.Date.AddDays(30)),
                DaysWithoutLostTimeIncident = CalculateDaysWithoutLostTimeIncident(incidents, today),
                Trir = CalculateRate(incidents.Count(x => x.IncidentType == "Accidente"), 200000m),
                Ltifr = CalculateRate(incidents.Count(x => x.LostTime), 1000000m),
                RecentIncidents = incidents.Take(8).ToList(),
                DueActions = actions.Where(x => x.Status != "Cerrada").Take(10).ToList(),
                LowScoreInspections = inspections.Where(x => x.ScorePercent < 85 || x.CriticalFindingsCount > 0).Take(8).ToList(),
                SiteSummaries = siteSummaries,
                CalendarItems = BuildCalendarItems(actions, inspections, trainings, permits, documents, nowUtc).Take(12).ToList(),
                TruckInspectionSummaries = truckInspectionSummaries
            };

            if (model.CriticalIncidents > 0)
            {
                model.Alerts.Add(new HsAlertViewModel { Tone = "danger", Message = $"{model.CriticalIncidents} incidente(s) criticos en el periodo." });
            }

            if (model.OverdueActions > 0)
            {
                model.Alerts.Add(new HsAlertViewModel { Tone = "danger", Message = $"{model.OverdueActions} accion(es) correctiva(s) vencidas." });
            }

            if (model.PendingInvestigations > 0)
            {
                model.Alerts.Add(new HsAlertViewModel { Tone = "warning", Message = $"{model.PendingInvestigations} investigacion(es) pendientes de causa raiz o cierre." });
            }

            if (model.PendingApprovals > 0)
            {
                model.Alerts.Add(new HsAlertViewModel { Tone = "warning", Message = $"{model.PendingApprovals} aprobacion(es) pendientes." });
            }

            if (model.OverduePermitApprovals > 0)
            {
                model.Alerts.Add(new HsAlertViewModel { Tone = "danger", Message = $"{model.OverduePermitApprovals} permiso(s) de trabajo superaron las 24 horas habiles de respuesta." });
            }

            if (model.ExpiredTrainings > 0)
            {
                model.Alerts.Add(new HsAlertViewModel { Tone = "danger", Message = $"{model.ExpiredTrainings} capacitacion(es) vencidas." });
            }

            if (model.DocumentsDueForReview > 0)
            {
                model.Alerts.Add(new HsAlertViewModel { Tone = "warning", Message = $"{model.DocumentsDueForReview} documento(s) por revisar en los proximos 30 dias." });
            }

            if (model.AverageInspectionScore > 0 && model.AverageInspectionScore < 85)
            {
                model.Alerts.Add(new HsAlertViewModel { Tone = "warning", Message = $"Cumplimiento promedio de inspecciones en {model.AverageInspectionScore:N2}%." });
            }

            if (model.Alerts.Count == 0)
            {
                model.Alerts.Add(new HsAlertViewModel { Tone = "success", Message = "Sin alertas criticas de H&S para el rango seleccionado." });
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Export(DateTime? startDate = null, DateTime? endDate = null)
        {
            var result = (ViewResult)await Dashboard(startDate, endDate);
            var model = (HsDashboardViewModel)result.Model!;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Resumen H&S");
            ws.Cells[1, 1].Value = "Indicador";
            ws.Cells[1, 2].Value = "Valor";
            ws.Cells[2, 1].Value = "Incidentes abiertos";
            ws.Cells[2, 2].Value = model.OpenIncidents;
            ws.Cells[3, 1].Value = "Incidentes criticos";
            ws.Cells[3, 2].Value = model.CriticalIncidents;
            ws.Cells[4, 1].Value = "Acciones abiertas";
            ws.Cells[4, 2].Value = model.OpenActions;
            ws.Cells[5, 1].Value = "Acciones vencidas";
            ws.Cells[5, 2].Value = model.OverdueActions;
            ws.Cells[6, 1].Value = "Inspecciones";
            ws.Cells[6, 2].Value = model.Inspections;
            ws.Cells[7, 1].Value = "Promedio inspeccion";
            ws.Cells[7, 2].Value = model.AverageInspectionScore;
            ws.Cells[8, 1].Value = "Personas capacitadas";
            ws.Cells[8, 2].Value = model.TrainedPeople;
            ws.Cells[1, 1, 1, 2].Style.Font.Bold = true;
            ws.Cells.AutoFitColumns();

            package.Workbook.Worksheets.Add("Incidentes").Cells[1, 1].LoadFromCollection(model.RecentIncidents, true);
            package.Workbook.Worksheets.Add("Acciones").Cells[1, 1].LoadFromCollection(model.DueActions, true);
            package.Workbook.Worksheets.Add("Sitios").Cells[1, 1].LoadFromCollection(model.SiteSummaries, true);

            var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"HS_{suffix}.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> Incidents(string? status = null)
        {
            var query = _context.HsIncidents.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.Status == status);
            }

            ViewBag.Status = status ?? string.Empty;
            ViewBag.StatusOptions = IncidentStatuses;
            return View(await query.OrderByDescending(x => x.OccurredAtUtc).Take(250).ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> IncidentDetails(int id)
        {
            var incident = await _context.HsIncidents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (incident == null)
            {
                return NotFound();
            }

            ViewBag.Actions = await _context.HsCorrectiveActions
                .AsNoTracking()
                .Where(x => x.SourceType == "Incidente" && x.SourceId == id)
                .OrderBy(x => x.DueDateUtc)
                .ToListAsync();
            return View(incident);
        }

        [HttpGet]
        public IActionResult IncidentCreate()
        {
            PopulateOptions();
            return View("IncidentEdit", new HsIncidentEditViewModel { ReportedBy = CurrentUserName() });
        }

        [HttpGet]
        public async Task<IActionResult> IncidentEdit(int id)
        {
            var entity = await _context.HsIncidents.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            PopulateOptions();
            return View(new HsIncidentEditViewModel
            {
                Id = entity.Id,
                OccurredAt = entity.OccurredAtUtc.ToLocalTime(),
                Site = entity.Site,
                Area = entity.Area,
                ReportedBy = entity.ReportedBy,
                IncidentType = entity.IncidentType,
                Severity = entity.Severity,
                Status = entity.Status,
                Description = entity.Description,
                ImmediateAction = entity.ImmediateAction,
                RootCause = entity.RootCause,
                ImmediateCause = entity.ImmediateCause,
                FiveWhyAnalysis = entity.FiveWhyAnalysis,
                Witnesses = entity.Witnesses,
                Investigator = entity.Investigator,
                InvestigationDueDate = entity.InvestigationDueDateUtc?.ToLocalTime().Date,
                RiskProbability = entity.RiskProbability,
                RiskSeverity = entity.RiskSeverity,
                ResidualRiskProbability = entity.ResidualRiskProbability,
                ResidualRiskSeverity = entity.ResidualRiskSeverity,
                CurrentControls = entity.CurrentControls,
                RecommendedControls = entity.RecommendedControls,
                LostTime = entity.LostTime,
                LostDays = entity.LostDays
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IncidentEdit(HsIncidentEditViewModel model)
        {
            PopulateOptions();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = model.Id.HasValue
                ? await _context.HsIncidents.FindAsync(model.Id.Value)
                : new HsIncident
                {
                    IncidentNumber = await GenerateNextNumberAsync("INC", _context.HsIncidents.Select(x => x.IncidentNumber)),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = CurrentUserName()
                };

            if (entity == null)
            {
                return NotFound();
            }

            entity.OccurredAtUtc = DateTime.SpecifyKind(model.OccurredAt, DateTimeKind.Local).ToUniversalTime();
            entity.Site = model.Site.Trim();
            entity.Area = model.Area.Trim();
            entity.ReportedBy = model.ReportedBy.Trim();
            entity.IncidentType = model.IncidentType.Trim();
            entity.Severity = model.Severity.Trim();
            entity.Status = model.Status.Trim();
            entity.Description = model.Description.Trim();
            entity.ImmediateAction = string.IsNullOrWhiteSpace(model.ImmediateAction) ? null : model.ImmediateAction.Trim();
            entity.RootCause = string.IsNullOrWhiteSpace(model.RootCause) ? null : model.RootCause.Trim();
            entity.ImmediateCause = string.IsNullOrWhiteSpace(model.ImmediateCause) ? null : model.ImmediateCause.Trim();
            entity.FiveWhyAnalysis = string.IsNullOrWhiteSpace(model.FiveWhyAnalysis) ? null : model.FiveWhyAnalysis.Trim();
            entity.Witnesses = string.IsNullOrWhiteSpace(model.Witnesses) ? null : model.Witnesses.Trim();
            entity.Investigator = string.IsNullOrWhiteSpace(model.Investigator) ? null : model.Investigator.Trim();
            entity.InvestigationDueDateUtc = model.InvestigationDueDate.HasValue ? DateTime.SpecifyKind(model.InvestigationDueDate.Value.Date, DateTimeKind.Utc) : null;
            entity.InvestigationClosedAtUtc = entity.Status == "Cerrado" && !string.IsNullOrWhiteSpace(entity.RootCause)
                ? DateTime.UtcNow
                : entity.InvestigationClosedAtUtc;
            entity.RiskProbability = model.RiskProbability;
            entity.RiskSeverity = model.RiskSeverity;
            entity.RiskScore = model.RiskProbability * model.RiskSeverity;
            entity.RiskLevel = ResolveRiskLevel(entity.RiskScore);
            entity.ResidualRiskProbability = model.ResidualRiskProbability;
            entity.ResidualRiskSeverity = model.ResidualRiskSeverity;
            entity.ResidualRiskScore = model.ResidualRiskProbability * model.ResidualRiskSeverity;
            entity.ResidualRiskLevel = ResolveRiskLevel(entity.ResidualRiskScore);
            entity.CurrentControls = string.IsNullOrWhiteSpace(model.CurrentControls) ? null : model.CurrentControls.Trim();
            entity.RecommendedControls = string.IsNullOrWhiteSpace(model.RecommendedControls) ? null : model.RecommendedControls.Trim();
            entity.LostTime = model.LostTime;
            entity.LostDays = model.LostTime ? model.LostDays : 0;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            entity.EvidencePath = await SaveEvidenceAsync(model.EvidenceFile, entity.EvidencePath, "incidents");

            if (!model.Id.HasValue)
            {
                _context.HsIncidents.Add(entity);
            }

            await _context.SaveChangesAsync();

            if (entity.Severity is "Alta" or "Critica" &&
                !await _context.HsCorrectiveActions.AnyAsync(x => x.SourceType == "Incidente" && x.SourceId == entity.Id))
            {
                _context.HsCorrectiveActions.Add(new HsCorrectiveAction
                {
                    SourceType = "Incidente",
                    SourceId = entity.Id,
                    Title = $"Investigar {entity.IncidentNumber}",
                    Description = $"Investigar causa raiz y plan de control para {entity.IncidentType} en {entity.Site} / {entity.Area}.",
                    Owner = entity.ReportedBy,
                    Priority = entity.Severity,
                    DueDateUtc = DateTime.UtcNow.Date.AddDays(entity.Severity == "Critica" ? 2 : 7),
                    CreatedBy = CurrentUserName(),
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            TempData["TicketsMessage"] = $"Incidente {entity.IncidentNumber} guardado.";
            return RedirectToAction(nameof(IncidentDetails), new { id = entity.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Inspections()
        {
            return View(await _context.HsInspections.AsNoTracking().OrderByDescending(x => x.InspectionDateUtc).Take(250).ToListAsync());
        }

        [HttpGet]
        public IActionResult InspectionCreate()
        {
            PopulateOptions();
            return View("InspectionEdit", new HsInspectionEditViewModel { Inspector = CurrentUserName() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InspectionEdit(HsInspectionEditViewModel model)
        {
            PopulateOptions();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = new HsInspection
            {
                InspectionNumber = await GenerateNextNumberAsync("INSP", _context.HsInspections.Select(x => x.InspectionNumber)),
                InspectionDateUtc = DateTime.SpecifyKind(model.InspectionDate.Date, DateTimeKind.Utc),
                Site = model.Site.Trim(),
                Area = model.Area.Trim(),
                Inspector = model.Inspector.Trim(),
                Category = model.Category.Trim(),
                ScorePercent = model.ScorePercent,
                FindingsCount = model.FindingsCount,
                CriticalFindingsCount = model.CriticalFindingsCount,
                Status = model.Status.Trim(),
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                CreatedBy = CurrentUserName(),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _context.HsInspections.Add(entity);
            await _context.SaveChangesAsync();

            if (entity.CriticalFindingsCount > 0 || entity.ScorePercent < 85)
            {
                _context.HsCorrectiveActions.Add(new HsCorrectiveAction
                {
                    SourceType = "Inspeccion",
                    SourceId = entity.Id,
                    Title = $"Cerrar hallazgos {entity.InspectionNumber}",
                    Description = $"Cerrar {entity.FindingsCount} hallazgo(s), {entity.CriticalFindingsCount} critico(s), de {entity.Category} en {entity.Site} / {entity.Area}.",
                    Owner = entity.Inspector,
                    Priority = entity.CriticalFindingsCount > 0 ? "Alta" : "Media",
                    DueDateUtc = DateTime.UtcNow.Date.AddDays(entity.CriticalFindingsCount > 0 ? 5 : 15),
                    CreatedBy = CurrentUserName(),
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            TempData["TicketsMessage"] = $"Inspeccion {entity.InspectionNumber} guardada.";
            return RedirectToAction(nameof(Inspections));
        }

        [HttpGet]
        public async Task<IActionResult> Actions(string? status = null)
        {
            await RefreshOverdueActionsAsync(DateTime.UtcNow);
            var query = _context.HsCorrectiveActions.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.Status == status);
            }

            ViewBag.Status = status ?? string.Empty;
            ViewBag.StatusOptions = ActionStatuses;
            return View(await query.OrderBy(x => x.Status == "Cerrada").ThenBy(x => x.DueDateUtc).Take(300).ToListAsync());
        }

        [HttpGet]
        public IActionResult ActionCreate()
        {
            PopulateOptions();
            return View("ActionEdit", new HsCorrectiveActionEditViewModel { Owner = CurrentUserName() });
        }

        [HttpGet]
        public async Task<IActionResult> ActionEdit(int id)
        {
            var entity = await _context.HsCorrectiveActions.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            PopulateOptions();
            return View(new HsCorrectiveActionEditViewModel
            {
                Id = entity.Id,
                SourceType = entity.SourceType,
                SourceId = entity.SourceId,
                Title = entity.Title,
                Description = entity.Description,
                Owner = entity.Owner,
                Priority = entity.Priority,
                Status = entity.Status,
                DueDate = entity.DueDateUtc.ToLocalTime().Date,
                ClosureNotes = entity.ClosureNotes
                ,
                ApprovalNotes = entity.ApprovalNotes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActionEdit(HsCorrectiveActionEditViewModel model)
        {
            PopulateOptions();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = model.Id.HasValue
                ? await _context.HsCorrectiveActions.FindAsync(model.Id.Value)
                : new HsCorrectiveAction { CreatedAtUtc = DateTime.UtcNow, CreatedBy = CurrentUserName() };
            if (entity == null)
            {
                return NotFound();
            }

            entity.SourceType = string.IsNullOrWhiteSpace(model.SourceType) ? "Manual" : model.SourceType.Trim();
            entity.SourceId = model.SourceId;
            entity.Title = model.Title.Trim();
            entity.Description = model.Description.Trim();
            entity.Owner = model.Owner.Trim();
            entity.Priority = model.Priority.Trim();
            entity.Status = model.Status.Trim();
            entity.DueDateUtc = DateTime.SpecifyKind(model.DueDate.Date, DateTimeKind.Utc);
            entity.ClosureNotes = string.IsNullOrWhiteSpace(model.ClosureNotes) ? null : model.ClosureNotes.Trim();
            entity.ApprovalNotes = string.IsNullOrWhiteSpace(model.ApprovalNotes) ? null : model.ApprovalNotes.Trim();
            if (entity.Status == "En revision")
            {
                entity.ReviewedBy = CurrentUserName();
                entity.ReviewedAtUtc = DateTime.UtcNow;
            }
            if (entity.Status is "Aprobada" or "Cerrada")
            {
                entity.ApprovedBy = CurrentUserName();
                entity.ApprovedAtUtc = DateTime.UtcNow;
                entity.ClosedAtUtc = DateTime.UtcNow;
            }
            else
            {
                entity.ClosedAtUtc = null;
            }
            entity.UpdatedAtUtc = DateTime.UtcNow;

            if (!model.Id.HasValue)
            {
                _context.HsCorrectiveActions.Add(entity);
            }

            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = "Accion correctiva guardada.";
            return RedirectToAction(nameof(Actions));
        }

        [HttpGet]
        public async Task<IActionResult> Trainings()
        {
            return View(await _context.HsTrainingRecords.AsNoTracking().OrderByDescending(x => x.TrainingDateUtc).Take(250).ToListAsync());
        }

        [HttpGet]
        public IActionResult TrainingCreate()
        {
            PopulateOptions();
            return View(new HsTrainingEditViewModel { Trainer = CurrentUserName() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrainingCreate(HsTrainingEditViewModel model)
        {
            PopulateOptions();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _context.HsTrainingRecords.Add(new HsTrainingRecord
            {
                TrainingDateUtc = DateTime.SpecifyKind(model.TrainingDate.Date, DateTimeKind.Utc),
                Topic = model.Topic.Trim(),
                Trainer = model.Trainer.Trim(),
                Site = model.Site.Trim(),
                Audience = model.Audience.Trim(),
                EmployeeName = string.IsNullOrWhiteSpace(model.EmployeeName) ? null : model.EmployeeName.Trim(),
                EmployeeCode = string.IsNullOrWhiteSpace(model.EmployeeCode) ? null : model.EmployeeCode.Trim(),
                Position = string.IsNullOrWhiteSpace(model.Position) ? null : model.Position.Trim(),
                AttendeeCount = model.AttendeeCount,
                RequiredTraining = model.RequiredTraining,
                ExpirationDateUtc = model.ExpirationDate.HasValue ? DateTime.SpecifyKind(model.ExpirationDate.Value.Date, DateTimeKind.Utc) : null,
                Result = model.Result.Trim(),
                EvidencePath = await SaveEvidenceAsync(model.EvidenceFile, null, "trainings"),
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                CreatedBy = CurrentUserName(),
                CreatedAtUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = "Capacitacion guardada.";
            return RedirectToAction(nameof(Trainings));
        }

        [HttpGet]
        public async Task<IActionResult> Permits()
        {
            return View(await _context.HsWorkPermits.AsNoTracking().OrderByDescending(x => x.StartAtUtc).Take(250).ToListAsync());
        }

        [HttpGet]
        public IActionResult PermitCreate()
        {
            PopulateOptions();
            return View("PermitEdit", new HsWorkPermitEditViewModel { RequestedBy = CurrentUserName() });
        }

        [HttpGet]
        public async Task<IActionResult> PermitEdit(int id)
        {
            var entity = await _context.HsWorkPermits.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            PopulateOptions();
            return View(new HsWorkPermitEditViewModel
            {
                Id = entity.Id,
                PermitType = entity.PermitType,
                Site = entity.Site,
                Area = entity.Area,
                RequestedBy = entity.RequestedBy,
                StartAt = entity.StartAtUtc.ToLocalTime(),
                EndAt = entity.EndAtUtc.ToLocalTime(),
                Status = entity.Status,
                Controls = entity.Controls,
                HasRiskAssessment = entity.HasRiskAssessment,
                HasAreaIsolation = entity.HasAreaIsolation,
                HasPpe = entity.HasPpe,
                HasEmergencyPlan = entity.HasEmergencyPlan,
                HasSupervisorApproval = entity.HasSupervisorApproval,
                ChecklistNotes = entity.ChecklistNotes,
                EvidencePaths = entity.EvidencePaths
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermitEdit(HsWorkPermitEditViewModel model)
        {
            PopulateOptions();
            if (model.EndAt <= model.StartAt)
            {
                ModelState.AddModelError(nameof(model.EndAt), "La fecha fin debe ser mayor que la fecha de inicio.");
            }

            ValidatePermitEvidenceFiles(model.EvidenceFiles);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = model.Id.HasValue
                ? await _context.HsWorkPermits.FindAsync(model.Id.Value)
                : new HsWorkPermit
                {
                    PermitNumber = await GenerateNextNumberAsync("PER", _context.HsWorkPermits.Select(x => x.PermitNumber)),
                    CreatedBy = CurrentUserName(),
                    CreatedAtUtc = DateTime.UtcNow
                };

            if (entity == null)
            {
                return NotFound();
            }

            entity.PermitType = model.PermitType.Trim();
            entity.Site = model.Site.Trim();
            entity.Area = model.Area.Trim();
            entity.RequestedBy = model.RequestedBy.Trim();
            entity.StartAtUtc = DateTime.SpecifyKind(model.StartAt, DateTimeKind.Local).ToUniversalTime();
            entity.EndAtUtc = DateTime.SpecifyKind(model.EndAt, DateTimeKind.Local).ToUniversalTime();
            entity.Status = model.Status.Trim();
            entity.Controls = string.IsNullOrWhiteSpace(model.Controls) ? null : model.Controls.Trim();
            entity.HasRiskAssessment = model.HasRiskAssessment;
            entity.HasAreaIsolation = model.HasAreaIsolation;
            entity.HasPpe = model.HasPpe;
            entity.HasEmergencyPlan = model.HasEmergencyPlan;
            entity.HasSupervisorApproval = model.HasSupervisorApproval;
            entity.ChecklistNotes = string.IsNullOrWhiteSpace(model.ChecklistNotes) ? null : model.ChecklistNotes.Trim();
            entity.EvidencePaths = await SaveEvidenceFilesAsync(model.EvidenceFiles, model.EvidencePaths, "permits");
            entity.ApprovalResponseDueAtUtc ??= AddBusinessHours(DateTime.UtcNow, 24);
            entity.UpdatedAtUtc = DateTime.UtcNow;

            if (entity.Status is "Aprobado" or "Rechazado" or "Solicita informacion extra")
            {
                entity.ApprovedBy = CurrentUserName();
                entity.ApprovedAtUtc = DateTime.UtcNow;
            }
            else if (entity.Status == "Solicitado" || entity.Status == "Rechazado")
            {
                entity.ApprovedBy = null;
                entity.ApprovedAtUtc = null;
            }

            if (!model.Id.HasValue)
            {
                _context.HsWorkPermits.Add(entity);
            }

            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = $"Permiso {entity.PermitNumber} guardado.";
            return RedirectToAction(nameof(Permits));
        }

        [HttpGet]
        public async Task<IActionResult> Documents()
        {
            return View(await _context.HsDocuments
                .AsNoTracking()
                .OrderBy(x => x.Status != "Vigente")
                .ThenBy(x => x.ReviewDateUtc)
                .Take(300)
                .ToListAsync());
        }

        [HttpGet]
        public IActionResult DocumentCreate()
        {
            PopulateOptions();
            return View(new HsDocumentEditViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DocumentCreate(HsDocumentEditViewModel model)
        {
            PopulateOptions();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var document = new HsDocument
            {
                Title = model.Title.Trim(),
                DocumentType = model.DocumentType.Trim(),
                Code = model.Code.Trim(),
                Version = string.IsNullOrWhiteSpace(model.Version) ? "1.0" : model.Version.Trim(),
                Site = string.IsNullOrWhiteSpace(model.Site) ? "General" : model.Site.Trim(),
                EffectiveDateUtc = DateTime.SpecifyKind(model.EffectiveDate.Date, DateTimeKind.Utc),
                ReviewDateUtc = model.ReviewDate.HasValue ? DateTime.SpecifyKind(model.ReviewDate.Value.Date, DateTimeKind.Utc) : null,
                Status = model.Status.Trim(),
                FilePath = await SaveEvidenceAsync(model.DocumentFile, null, "documents"),
                CreatedBy = CurrentUserName(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.HsDocuments.Add(document);
            await _context.SaveChangesAsync();
            TempData["TicketsMessage"] = $"Documento {document.Code} guardado.";
            return RedirectToAction(nameof(Documents));
        }

        [HttpGet]
        public async Task<IActionResult> Calendar()
        {
            var nowUtc = DateTime.UtcNow;
            var actions = await _context.HsCorrectiveActions.AsNoTracking().Where(x => x.Status != "Cerrada").ToListAsync();
            var inspections = await _context.HsInspections.AsNoTracking().Where(x => x.InspectionDateUtc >= nowUtc.AddDays(-7)).ToListAsync();
            var trainings = await _context.HsTrainingRecords.AsNoTracking().Where(x => x.ExpirationDateUtc >= nowUtc.AddDays(-30)).ToListAsync();
            var permits = await _context.HsWorkPermits.AsNoTracking().Where(x => x.EndAtUtc >= nowUtc.AddDays(-7)).ToListAsync();
            var documents = await _context.HsDocuments.AsNoTracking().Where(x => x.ReviewDateUtc >= nowUtc.AddDays(-30)).ToListAsync();

            var items = BuildCalendarItems(actions, inspections, trainings, permits, documents, nowUtc)
                .OrderBy(x => x.Date)
                .Take(200)
                .ToList();

            if (items.Count == 0)
            {
                items = BuildDefaultCalendarItems(DateTime.Today).ToList();
                ViewBag.CalendarFallback = true;
            }

            return View(items);
        }

        private async Task RefreshOverdueActionsAsync(DateTime nowUtc)
        {
            var overdue = await _context.HsCorrectiveActions
                .Where(x => x.Status != "Cerrada" && x.Status != "Aprobada" && x.DueDateUtc.Date < nowUtc.Date)
                .ToListAsync();

            if (overdue.Count == 0)
            {
                return;
            }

            foreach (var action in overdue)
            {
                action.Status = "Vencida";
                action.UpdatedAtUtc = nowUtc;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<string> GenerateNextNumberAsync(string prefix, IQueryable<string> existingNumbers)
        {
            var numberPrefix = $"{prefix}-{DateTime.UtcNow:yyyyMM}";
            var last = await existingNumbers
                .Where(x => EF.Functions.Like(x, numberPrefix + "-%"))
                .OrderByDescending(x => x)
                .FirstOrDefaultAsync();

            var next = 1;
            if (!string.IsNullOrWhiteSpace(last))
            {
                var index = last.LastIndexOf('-');
                if (index >= 0 && int.TryParse(last[(index + 1)..], out var parsed))
                {
                    next = parsed + 1;
                }
            }

            return $"{numberPrefix}-{next:D4}";
        }

        private static bool IsPermitWaitingForApprovalResponse(HsWorkPermit permit)
        {
            return permit.Status == "Solicitado";
        }

        private static DateTime AddBusinessHours(DateTime startUtc, double hours)
        {
            var remaining = hours;
            var cursor = startUtc.ToLocalTime();

            while (remaining > 0)
            {
                cursor = MoveToBusinessTime(cursor);
                var window = GetBusinessWindow(cursor.Date);
                if (window == null)
                {
                    cursor = cursor.Date.AddDays(1).AddHours(7.5);
                    continue;
                }

                var available = (window.Value.End - cursor).TotalHours;
                var used = Math.Min(remaining, available);
                cursor = cursor.AddHours(used);
                remaining -= used;

                if (remaining > 0)
                {
                    cursor = cursor.Date.AddDays(1).AddHours(7.5);
                }
            }

            return cursor.ToUniversalTime();
        }

        private static DateTime MoveToBusinessTime(DateTime value)
        {
            var cursor = value;
            while (true)
            {
                var window = GetBusinessWindow(cursor.Date);
                if (window == null)
                {
                    cursor = cursor.Date.AddDays(1).AddHours(7.5);
                    continue;
                }

                if (cursor < window.Value.Start)
                {
                    return window.Value.Start;
                }

                if (cursor >= window.Value.End)
                {
                    cursor = cursor.Date.AddDays(1).AddHours(7.5);
                    continue;
                }

                return cursor;
            }
        }

        private static (DateTime Start, DateTime End)? GetBusinessWindow(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                return null;
            }

            var start = date.AddHours(7.5);
            var end = date.DayOfWeek == DayOfWeek.Saturday
                ? date.AddHours(13)
                : date.AddHours(17);

            return (start, end);
        }

        private async Task<string?> SaveEvidenceAsync(IFormFile? file, string? existingPath, string folder)
        {
            if (file == null || file.Length == 0)
            {
                return existingPath;
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                return existingPath;
            }

            var root = Path.Combine(_environment.WebRootPath, "uploads", "hs", folder);
            Directory.CreateDirectory(root);
            var safeName = Path.GetFileName(file.FileName);
            var storedName = $"{Guid.NewGuid():N}_{safeName}";
            var fullPath = Path.Combine(root, storedName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/hs/{folder}/{storedName}";
        }

        private async Task<string?> SaveEvidenceFilesAsync(IEnumerable<IFormFile>? files, string? existingPaths, string folder)
        {
            var paths = SplitStoredPaths(existingPaths).ToList();
            if (files == null)
            {
                return paths.Count == 0 ? null : string.Join("|", paths);
            }

            foreach (var file in files.Where(x => x.Length > 0))
            {
                var savedPath = await SaveEvidenceAsync(file, null, folder);
                if (!string.IsNullOrWhiteSpace(savedPath))
                {
                    paths.Add(savedPath);
                }
            }

            return paths.Count == 0 ? null : string.Join("|", paths.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private void ValidatePermitEvidenceFiles(IEnumerable<IFormFile>? files)
        {
            if (files == null)
            {
                return;
            }

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

            foreach (var file in files.Where(x => x.Length > 0))
            {
                var extension = Path.GetExtension(file.FileName);
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(nameof(HsWorkPermitEditViewModel.EvidenceFiles), "Las evidencias deben ser imagenes JPG, PNG o WEBP.");
                    return;
                }
            }
        }

        private static IEnumerable<string> SplitStoredPaths(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Enumerable.Empty<string>()
                : value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private void PopulateOptions()
        {
            ViewBag.SiteOptions = Sites;
            ViewBag.IncidentTypeOptions = IncidentTypes;
            ViewBag.SeverityOptions = Severities;
            ViewBag.IncidentStatusOptions = IncidentStatuses;
            ViewBag.ActionStatusOptions = ActionStatuses;
            ViewBag.PriorityOptions = Priorities;
            ViewBag.PermitStatusOptions = PermitStatuses;
            ViewBag.PermitTypeOptions = PermitTypes;
            ViewBag.DocumentTypeOptions = DocumentTypes;
            ViewBag.DocumentStatusOptions = DocumentStatuses;
        }

        private static string ResolveRiskLevel(int score)
        {
            return score switch
            {
                >= 20 => "Critico",
                >= 15 => "Alto",
                >= 8 => "Medio",
                _ => "Bajo"
            };
        }

        private static decimal CalculateRate(int cases, decimal multiplier)
        {
            const decimal defaultHours = 200000m;
            return cases == 0 ? 0 : Math.Round(cases * multiplier / defaultHours, 2);
        }

        private static int CalculateDaysWithoutLostTimeIncident(IEnumerable<HsIncident> incidents, DateTime today)
        {
            var last = incidents
                .Where(x => x.LostTime)
                .OrderByDescending(x => x.OccurredAtUtc)
                .FirstOrDefault();

            return last == null ? 0 : Math.Max(0, (today - last.OccurredAtUtc.ToLocalTime().Date).Days);
        }

        private static IEnumerable<HsCalendarItemViewModel> BuildCalendarItems(
            IEnumerable<HsCorrectiveAction> actions,
            IEnumerable<HsInspection> inspections,
            IEnumerable<HsTrainingRecord> trainings,
            IEnumerable<HsWorkPermit> permits,
            IEnumerable<HsDocument> documents,
            DateTime nowUtc)
        {
            foreach (var action in actions.Where(x => x.Status != "Cerrada" && x.Status != "Aprobada"))
            {
                yield return new HsCalendarItemViewModel
                {
                    Date = action.DueDateUtc.ToLocalTime().Date,
                    Type = "Accion",
                    Title = action.Title,
                    Tone = action.DueDateUtc.Date < nowUtc.Date ? "danger" : "warning"
                };
            }

            foreach (var inspection in inspections)
            {
                yield return new HsCalendarItemViewModel
                {
                    Date = inspection.InspectionDateUtc.ToLocalTime().Date,
                    Type = "Inspeccion",
                    Title = $"{inspection.Category} - {inspection.Site}",
                    Tone = inspection.ScorePercent < 85 ? "warning" : "success"
                };
            }

            foreach (var training in trainings.Where(x => x.ExpirationDateUtc.HasValue))
            {
                yield return new HsCalendarItemViewModel
                {
                    Date = training.ExpirationDateUtc!.Value.ToLocalTime().Date,
                    Type = "Capacitacion",
                    Title = $"{training.Topic} - {training.EmployeeName ?? training.Audience}",
                    Tone = training.ExpirationDateUtc.Value.Date < nowUtc.Date ? "danger" : "info"
                };
            }

            foreach (var permit in permits)
            {
                yield return new HsCalendarItemViewModel
                {
                    Date = permit.EndAtUtc.ToLocalTime().Date,
                    Type = "Permiso",
                    Title = $"{permit.PermitType} - {permit.Area}",
                    Tone = permit.Status == "Rechazado" ? "danger" : "primary"
                };
            }

            foreach (var document in documents.Where(x => x.ReviewDateUtc.HasValue))
            {
                yield return new HsCalendarItemViewModel
                {
                    Date = document.ReviewDateUtc!.Value.ToLocalTime().Date,
                    Type = "Documento",
                    Title = $"{document.Code} - {document.Title}",
                    Tone = document.ReviewDateUtc.Value.Date <= nowUtc.Date ? "warning" : "secondary"
                };
            }
        }

        private static IEnumerable<HsCalendarItemViewModel> BuildDefaultCalendarItems(DateTime today)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1);
            yield return new HsCalendarItemViewModel { Date = today, Type = "Inspeccion", Title = "Inspeccion general de areas operativas", Tone = "success" };
            yield return new HsCalendarItemViewModel { Date = today.AddDays(2), Type = "Capacitacion", Title = "Charla de seguridad: reporte de actos y condiciones inseguras", Tone = "info" };
            yield return new HsCalendarItemViewModel { Date = today.AddDays(5), Type = "Accion", Title = "Revision semanal de acciones correctivas abiertas", Tone = "warning" };
            yield return new HsCalendarItemViewModel { Date = today.AddDays(7), Type = "Permiso", Title = "Revision de permisos de trabajo de alto riesgo", Tone = "primary" };
            yield return new HsCalendarItemViewModel { Date = monthStart.AddDays(14), Type = "Documento", Title = "Revision mensual de matriz IPER y AST/JSA", Tone = "secondary" };
            yield return new HsCalendarItemViewModel { Date = monthStart.AddMonths(1).AddDays(-1), Type = "Auditoria", Title = "Cierre mensual H&S y reporte gerencial", Tone = "dark" };
        }

        private string CurrentUserName()
        {
            return string.IsNullOrWhiteSpace(User?.Identity?.Name)
                ? "Sistema"
                : User.Identity!.Name!.Trim();
        }
    }
}
