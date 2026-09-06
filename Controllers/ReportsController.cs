using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class ReportsController : Controller
    {
        private const string AllValue = "all";
        private static readonly string[] AllowedTabs = { "summary", "technician", "area", "site", "finance" };
        private const decimal LaborCostPerHourCordoba = 350m;
        private const decimal FinanceUsdToCordobaRate = 36.5m;

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ReportsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? startDate,
            DateTime? endDate,
            string? technician,
            string? area,
            string? areaFilter,
            string? site,
            string? tab,
            bool printMode = false)
        {
            var effectiveArea = string.IsNullOrWhiteSpace(areaFilter) ? area : areaFilter;
            var report = await BuildReportAsync(startDate, endDate, technician, effectiveArea, site, tab, printMode);
            return View(report.Model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            DateTime? startDate,
            DateTime? endDate,
            string? technician,
            string? area,
            string? areaFilter,
            string? site,
            string? tab)
        {
            var effectiveArea = string.IsNullOrWhiteSpace(areaFilter) ? area : areaFilter;
            var report = await BuildReportAsync(startDate, endDate, technician, effectiveArea, site, tab, printMode: false);

            var csv = new StringBuilder();
            csv.AppendLine("Ticket,Fecha Creacion,Fecha Cierre,Estado,Prioridad,Tecnico,Area,Sitio,Tipo,SLA,Estado SLA,Primera Respuesta (h),Resolucion (h),Reaperturas,Costo C$,Costo USD");

            foreach (var t in report.Tickets.OrderByDescending(x => x.CreatedDate))
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(t.TicketNumber),
                    EscapeCsv(t.CreatedDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                    EscapeCsv(t.ClosedDate?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty),
                    EscapeCsv(t.Status.ToString()),
                    EscapeCsv(t.Priority.ToString()),
                    EscapeCsv(LabelOrDefault(t.AssignedTechnician, "Sin asignar")),
                    EscapeCsv(LabelOrDefault(t.Department, "Sin definir")),
                    EscapeCsv(LabelOrDefault(t.Site, "Sin definir")),
                    EscapeCsv(LabelOrDefault(t.IncidentType, "Otros")),
                    EscapeCsv(t.SLADeadline.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                    EscapeCsv(t.IsOverdue ? "Vencido" : t.IsNearDue ? "Por vencer" : "En tiempo"),
                    EscapeCsv(t.FirstResponseHours?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty),
                    EscapeCsv(t.ResolutionHours?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty),
                    EscapeCsv(t.ReopenedCount.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(t.ComponentCostCordoba.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(t.ComponentCostUsd.ToString("0.00", CultureInfo.InvariantCulture))));
            }

            var fileName = $"ReporteTickets_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());

            return File(bytes, "text/csv", fileName);
        }


        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            DateTime? startDate,
            DateTime? endDate,
            string? technician,
            string? area,
            string? areaFilter,
            string? site,
            string? tab)
        {
            var effectiveArea = string.IsNullOrWhiteSpace(areaFilter) ? area : areaFilter;
            var report = await BuildReportAsync(startDate, endDate, technician, effectiveArea, site, tab, printMode: false);

            var rows = report.Tickets
                .Select(t => new ReportsPdfTicketRow(
                    TicketNumber: string.IsNullOrWhiteSpace(t.TicketNumber) ? $"TK-{t.Id:D4}" : t.TicketNumber,
                    CreatedDate: t.CreatedDate,
                    StatusLabel: t.Status switch
                    {
                        TicketStatus.Open => "Abierto",
                        TicketStatus.InProgress => "En Progreso",
                        TicketStatus.Resolved => "Resuelto",
                        TicketStatus.Closed => "Cerrado",
                        _ => t.Status.ToString()
                    },
                    PriorityLabel: t.Priority switch
                    {
                        PriorityLevel.Low => "Baja",
                        PriorityLevel.Medium => "Media",
                        PriorityLevel.High => "Alta",
                        PriorityLevel.Critical => "Critica",
                        _ => t.Priority.ToString()
                    },
                    Technician: LabelOrDefault(t.AssignedTechnician, "Sin asignar"),
                    Area: LabelOrDefault(t.Department, "Sin definir"),
                    Site: LabelOrDefault(t.Site, "Sin definir")))
                .ToList();

            var bytes = ReportsPdfReportService.GenerateReportPdf(report.Model, rows, _environment.WebRootPath);
            var fileName = $"ReporteTickets_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> ExportFinanceErp(
            DateTime? startDate,
            DateTime? endDate,
            string? technician,
            string? area,
            string? areaFilter,
            string? site,
            string? tab)
        {
            var effectiveArea = string.IsNullOrWhiteSpace(areaFilter) ? area : areaFilter;
            var report = await BuildReportAsync(startDate, endDate, technician, effectiveArea, site, tab, printMode: false);

            if (!report.Model.IsFinanceArea)
            {
                return BadRequest("La exportación ERP está disponible solo para el área de Finanzas.");
            }

            var rows = report.Tickets
                .Where(t => string.Equals(t.Department, "Finanzas", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.CreatedDate)
                .ToList();

            var csv = new StringBuilder();
            csv.AppendLine("Ticket,Fecha Creacion,Solicitante,Tipo Solicitud,Proveedor,Centro Costo,Moneda,Monto Solicitud,Estado,Prioridad,Asiento Contable,Orden Compra,Factura,Fecha Pago,Metodo Pago,Conciliacion,Riesgo Presupuestario,ERP Exportado,Referencia ERP,Fecha Cierre");

            foreach (var ticket in rows)
            {
                var meta = ParseFinanceReportMeta(ticket.Description);
                var amount = decimal.Round(meta.Amount, 2, MidpointRounding.AwayFromZero);
                var budgetRisk = meta.HasBudgetRisk ? "Sobregiro" : "Dentro de presupuesto";
                var erpExported = ticket.FinanceErpExportedAtUtc.HasValue ? "Si" : "No";

                csv.AppendLine(string.Join(",",
                    EscapeCsv(string.IsNullOrWhiteSpace(ticket.TicketNumber) ? $"TK-{ticket.Id:D4}" : ticket.TicketNumber),
                    EscapeCsv(ticket.CreatedDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                    EscapeCsv(LabelOrDefault(ticket.RequestingUser, "Sin solicitante")),
                    EscapeCsv(LabelOrDefault(ticket.IncidentType, "Solicitud financiera")),
                    EscapeCsv(LabelOrDefault(meta.VendorName, "Sin proveedor")),
                    EscapeCsv(LabelOrDefault(meta.CostCenter, "General")),
                    EscapeCsv(string.IsNullOrWhiteSpace(meta.Currency) ? "C$" : meta.Currency),
                    EscapeCsv(amount.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(FinanceStatusLabel(ticket.Status)),
                    EscapeCsv(ticket.Priority.ToString()),
                    EscapeCsv(LabelOrDefault(ticket.FinanceAccountingEntryNumber, string.Empty)),
                    EscapeCsv(LabelOrDefault(ticket.FinancePurchaseOrderNumber, string.Empty)),
                    EscapeCsv(LabelOrDefault(ticket.FinanceInvoiceNumber, string.Empty)),
                    EscapeCsv(ticket.FinancePaymentDateUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty),
                    EscapeCsv(LabelOrDefault(ticket.FinanceFinalPaymentMethod, string.Empty)),
                    EscapeCsv(LabelOrDefault(ticket.FinanceReconciliationStatus, "Pendiente")),
                    EscapeCsv(budgetRisk),
                    EscapeCsv(erpExported),
                    EscapeCsv(LabelOrDefault(ticket.FinanceErpExportReference, string.Empty)),
                    EscapeCsv(ticket.ClosedDate?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty)));
            }

            var fileName = $"ReporteFinanzasERP_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", fileName);
        }

        private async Task<(ReportIndexViewModel Model, List<ReportTicketRow> Tickets)> BuildReportAsync(
            DateTime? startDate,
            DateTime? endDate,
            string? technician,
            string? area,
            string? site,
            string? tab,
            bool printMode)
        {
            var utcNow = DateTime.UtcNow;

            var safeStartDate = (startDate ?? new DateTime(utcNow.Year, utcNow.Month, 1)).Date;
            var safeEndDate = (endDate ?? utcNow.Date).Date;

            if (safeEndDate < safeStartDate)
            {
                (safeStartDate, safeEndDate) = (safeEndDate, safeStartDate);
            }

            var normalizedArea = NormalizeFilter(area);
            var normalizedSite = NormalizeFilter(site);
            var isFinanceArea = string.Equals(normalizedArea, "Finanzas", StringComparison.OrdinalIgnoreCase);
            var normalizedTechnician = isFinanceArea
                ? AllValue
                : NormalizeFilter(technician);

            var selectedTab = NormalizeTab(tab);
            if (isFinanceArea && string.IsNullOrWhiteSpace(tab))
            {
                selectedTab = "finance";
            }

            var startInclusiveUtc = DateTime.SpecifyKind(safeStartDate, DateTimeKind.Utc);
            var endExclusiveUtc = DateTime.SpecifyKind(safeEndDate.AddDays(1), DateTimeKind.Utc);

            var filteredBaseQuery = _context.Tickets.AsNoTracking();

            if (normalizedTechnician != AllValue)
            {
                if (normalizedTechnician == "unassigned")
                {
                    filteredBaseQuery = filteredBaseQuery.Where(t => string.IsNullOrWhiteSpace(t.AssignedTechnician));
                }
                else
                {
                    filteredBaseQuery = filteredBaseQuery.Where(t => t.AssignedTechnician == normalizedTechnician);
                }
            }

            if (normalizedArea != AllValue)
            {
                filteredBaseQuery = filteredBaseQuery.Where(t => t.Department == normalizedArea);
            }

            if (normalizedSite != AllValue)
            {
                filteredBaseQuery = filteredBaseQuery.Where(t => t.Site == normalizedSite);
            }

            var ticketsQuery = filteredBaseQuery
                .Where(t => t.CreatedDate >= startInclusiveUtc && t.CreatedDate < endExclusiveUtc);

            var tickets = await ticketsQuery
                .Select(t => new ReportTicketRow
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber ?? string.Empty,
                    RequestingUser = t.RequestingUser,
                    CreatedDate = t.CreatedDate,
                    ClosedDate = t.ClosedDate,
                    Status = t.Status,
                    Priority = t.Priority,
                    AssignedTechnician = t.AssignedTechnician,
                    Department = t.Department,
                    Site = t.Site,
                    IncidentType = t.IncidentType,
                    SLADeadline = t.SLADeadline,
                    Description = t.Description,
                    FinanceAccountingEntryNumber = t.FinanceAccountingEntryNumber,
                    FinancePurchaseOrderNumber = t.FinancePurchaseOrderNumber,
                    FinanceInvoiceNumber = t.FinanceInvoiceNumber,
                    FinancePaymentDateUtc = t.FinancePaymentDateUtc,
                    FinanceFinalPaymentMethod = t.FinanceFinalPaymentMethod,
                    FinanceReconciliationStatus = t.FinanceReconciliationStatus,
                    FinanceErpExportedAtUtc = t.FinanceErpExportedAtUtc,
                    FinanceErpExportReference = t.FinanceErpExportReference,
                    ComponentCostCordoba = (t.ChangedComponentCostCordoba ?? t.ChangedComponentCost ?? 0),
                    ComponentCostUsd = (t.ChangedComponentCostUsd ?? 0)
                })
                .ToListAsync();

            var ticketIds = tickets.Select(t => t.Id).ToList();
            var histories = ticketIds.Count == 0
                ? new List<TicketHistory>()
                : await _context.TicketHistories
                    .AsNoTracking()
                    .Where(h => ticketIds.Contains(h.TicketId))
                    .OrderBy(h => h.ChangeDate)
                    .ToListAsync();

            var historiesByTicket = histories
                .GroupBy(h => h.TicketId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var ticket in tickets)
            {
                ticket.ResolutionHours = ticket.ClosedDate.HasValue
                    ? Math.Round((ticket.ClosedDate.Value - ticket.CreatedDate).TotalHours, 1)
                    : null;

                ticket.IsOverdue = ticket.Status != TicketStatus.Closed && ticket.SLADeadline < utcNow;
                ticket.IsNearDue = ticket.Status != TicketStatus.Closed && !ticket.IsOverdue && ticket.SLADeadline <= utcNow.AddHours(4);
                ticket.IsUnassigned = string.IsNullOrWhiteSpace(ticket.AssignedTechnician);

                if (historiesByTicket.TryGetValue(ticket.Id, out var ticketHistory))
                {
                    var firstResponse = ticketHistory.FirstOrDefault(IsFirstResponseChange);
                    if (firstResponse != null)
                    {
                        ticket.FirstResponseHours = Math.Round((firstResponse.ChangeDate - ticket.CreatedDate).TotalHours, 1);
                    }

                    ticket.ReopenedCount = ticketHistory.Count(IsReopenChange);
                }
            }

            var totalTickets = tickets.Count;
            var createdTickets = totalTickets;
            var closedTickets = await filteredBaseQuery.CountAsync(t =>
                t.ClosedDate.HasValue &&
                t.ClosedDate.Value >= startInclusiveUtc &&
                t.ClosedDate.Value < endExclusiveUtc);
            var resolvedTickets = tickets.Count(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed);
            var inProgressTickets = tickets.Count(t => t.Status == TicketStatus.InProgress);
            var pendingTickets = tickets.Count(t => t.Status == TicketStatus.Open);
            var overdueTickets = tickets.Count(t => t.IsOverdue);
            var nearDueTickets = tickets.Count(t => t.IsNearDue);
            var unassignedTickets = tickets.Count(t => t.IsUnassigned);
            var criticalTickets = tickets.Count(t => t.Priority == PriorityLevel.Critical);
            var reopenedTickets = tickets.Sum(t => t.ReopenedCount);

            var closedForAverage = tickets
                .Where(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed)
                .Where(t => t.ClosedDate.HasValue)
                .ToList();

            var avgResolutionHours = closedForAverage.Any()
                ? Math.Round(closedForAverage.Average(t => (t.ClosedDate!.Value - t.CreatedDate).TotalHours), 1)
                : 0;

            var avgFirstResponseHours = tickets
                .Where(t => t.FirstResponseHours.HasValue && t.FirstResponseHours.Value >= 0)
                .Select(t => t.FirstResponseHours!.Value)
                .DefaultIfEmpty()
                .Average();

            if (double.IsNaN(avgFirstResponseHours))
            {
                avgFirstResponseHours = 0;
            }

            avgFirstResponseHours = Math.Round(avgFirstResponseHours, 1);

            var slaCompliantCount = tickets.Count(t => IsSlaCompliant(t, utcNow));
            var slaCompliancePercent = totalTickets > 0
                ? (int)Math.Round((double)slaCompliantCount * 100 / totalTickets, MidpointRounding.AwayFromZero)
                : 0;

            var resolvedRate = totalTickets > 0
                ? (double)resolvedTickets * 100 / totalTickets
                : 0;

            var satisfaction = (int)Math.Round((resolvedRate * 0.7) + (slaCompliancePercent * 0.3), MidpointRounding.AwayFromZero);
            satisfaction = Math.Clamp(satisfaction, 0, 100);

            var periodLengthDays = (safeEndDate - safeStartDate).Days + 1;
            var previousPeriodStartDate = safeStartDate.AddDays(-periodLengthDays);
            var previousPeriodStartUtc = DateTime.SpecifyKind(previousPeriodStartDate, DateTimeKind.Utc);
            var previousPeriodEndExclusiveUtc = startInclusiveUtc;

            var currentPeriodTickets = totalTickets;
            var previousPeriodTickets = await filteredBaseQuery.CountAsync(t =>
                t.CreatedDate >= previousPeriodStartUtc && t.CreatedDate < previousPeriodEndExclusiveUtc);

            var currentYearStartUtc = new DateTime(safeEndDate.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var currentYearEndExclusiveUtc = endExclusiveUtc;
            var previousYearStartUtc = currentYearStartUtc.AddYears(-1);
            var previousYearEndExclusiveUtc = currentYearEndExclusiveUtc.AddYears(-1);

            var currentYearTickets = await filteredBaseQuery.CountAsync(t =>
                t.CreatedDate >= currentYearStartUtc && t.CreatedDate < currentYearEndExclusiveUtc);
            var previousYearTickets = await filteredBaseQuery.CountAsync(t =>
                t.CreatedDate >= previousYearStartUtc && t.CreatedDate < previousYearEndExclusiveUtc);

            var activeBacklog = await filteredBaseQuery.CountAsync(t =>
                t.CreatedDate < endExclusiveUtc &&
                (!t.ClosedDate.HasValue || t.ClosedDate.Value >= endExclusiveUtc));
            var backlogDelta = createdTickets - closedTickets;

            var totalComponentCostCordoba = tickets.Sum(t => t.ComponentCostCordoba);
            var totalComponentCostUsd = tickets.Sum(t => t.ComponentCostUsd);
            var estimatedLaborCostCordoba = Math.Round(closedForAverage.Sum(t => (decimal)(t.ResolutionHours ?? 0) * LaborCostPerHourCordoba), 2);
            var estimatedTotalCostCordoba = totalComponentCostCordoba + estimatedLaborCostCordoba;

            var distributionByType = BuildGroupData(
                tickets,
                t => LabelOrDefault(t.IncidentType, "Otros"),
                totalTickets);

            var byTechnician = BuildGroupData(
                tickets,
                t => LabelOrDefault(t.AssignedTechnician, "Sin asignar"),
                totalTickets);

            var byArea = BuildGroupData(
                tickets,
                t => LabelOrDefault(t.Department, "Sin definir"),
                totalTickets);

            var bySite = BuildGroupData(
                tickets,
                t => LabelOrDefault(t.Site, "Sin definir"),
                totalTickets);

            if (!isFinanceArea && selectedTab == "finance")
            {
                selectedTab = "summary";
            }
            else if (isFinanceArea && selectedTab == "technician")
            {
                selectedTab = "summary";
            }

            var financeRows = tickets
                .Where(t => string.Equals(t.Department, "Finanzas", StringComparison.OrdinalIgnoreCase))
                .Select(t =>
                {
                    var meta = ParseFinanceReportMeta(t.Description);
                    return new
                    {
                        RequestingUser = LabelOrDefault(t.RequestingUser, "Sin solicitante"),
                        Vendor = LabelOrDefault(meta.VendorName, "Sin proveedor"),
                        CostCenter = LabelOrDefault(meta.CostCenter, "General"),
                        RawStatus = t.Status,
                        Status = FinanceStatusLabel(t.Status),
                        AmountCordoba = ConvertFinanceToCordoba(meta.Amount, meta.Currency),
                        HasBudgetRisk = meta.HasBudgetRisk
                    };
                })
                .ToList();

            var financeTotalRequestedCordoba = financeRows.Sum(x => x.AmountCordoba);
            var financeAverageRequestedCordoba = financeRows.Count > 0
                ? decimal.Round(financeTotalRequestedCordoba / financeRows.Count, 2, MidpointRounding.AwayFromZero)
                : 0m;
            var financeRequestsWithBudgetRisk = financeRows.Count(x => x.HasBudgetRisk);

            var financeByRequester = BuildGroupDataFromLabels(
                financeRows.Select(x => x.RequestingUser),
                financeRows.Count);
            var financeByVendor = BuildGroupDataFromLabels(
                financeRows.Select(x => x.Vendor),
                financeRows.Count);
            var financeByCostCenter = BuildGroupDataFromLabels(
                financeRows.Select(x => x.CostCenter),
                financeRows.Count);
            var financeByStatus = BuildGroupDataFromLabels(
                financeRows.Select(x => x.Status),
                financeRows.Count);

            var financeRequesterPerformance = financeRows
                .GroupBy(x => x.RequestingUser)
                .Select(g => new ReportFinanceRequesterPerformanceViewModel
                {
                    Requester = g.Key,
                    TotalRequests = g.Count(),
                    PendingRequests = g.Count(x => x.RawStatus == TicketStatus.Open),
                    InReviewRequests = g.Count(x => x.RawStatus == TicketStatus.InProgress),
                    CloseReadyRequests = g.Count(x => x.RawStatus == TicketStatus.Resolved),
                    ClosedRequests = g.Count(x => x.RawStatus == TicketStatus.Closed),
                    BudgetRiskRequests = g.Count(x => x.HasBudgetRisk),
                    RequestedAmountCordoba = decimal.Round(g.Sum(x => x.AmountCordoba), 2, MidpointRounding.AwayFromZero)
                })
                .OrderByDescending(x => x.TotalRequests)
                .ThenByDescending(x => x.RequestedAmountCordoba)
                .ThenBy(x => x.Requester)
                .ToList();

            var technicianPerformance = tickets
                .GroupBy(t => LabelOrDefault(t.AssignedTechnician, "Sin asignar"))
                .Select(g =>
                {
                    var groupTotal = g.Count();
                    var groupResolved = g.Count(x => x.Status is TicketStatus.Resolved or TicketStatus.Closed);
                    var groupClosed = g.Where(x => x.ResolutionHours.HasValue).Select(x => x.ResolutionHours!.Value).ToList();

                    return new ReportTechnicianPerformanceViewModel
                    {
                        Technician = g.Key,
                        TotalTickets = groupTotal,
                        ResolvedTickets = groupResolved,
                        OverdueTickets = g.Count(x => x.IsOverdue),
                        ReopenedTickets = g.Sum(x => x.ReopenedCount),
                        AverageResolutionHours = groupClosed.Count > 0 ? Math.Round(groupClosed.Average(), 1) : 0,
                        SlaCompliancePercent = groupTotal > 0
                            ? Math.Round((double)g.Count(x => IsSlaCompliant(x, utcNow)) * 100 / groupTotal, 1)
                            : 0
                    };
                })
                .OrderByDescending(x => x.TotalTickets)
                .ThenByDescending(x => x.OverdueTickets)
                .ThenBy(x => x.Technician)
                .ToList();

            var alerts = BuildAlerts(
                slaCompliancePercent,
                overdueTickets,
                nearDueTickets,
                unassignedTickets,
                backlogDelta);

            var model = new ReportIndexViewModel
            {
                StartDate = safeStartDate,
                EndDate = safeEndDate,
                SelectedTechnician = normalizedTechnician,
                SelectedArea = normalizedArea,
                SelectedSite = normalizedSite,
                ActiveTab = selectedTab,
                PrintMode = printMode,
                TotalTickets = totalTickets,
                CreatedTickets = createdTickets,
                ClosedTickets = closedTickets,
                ResolvedTickets = resolvedTickets,
                InProgressTickets = inProgressTickets,
                PendingTickets = pendingTickets,
                OverdueTickets = overdueTickets,
                NearDueTickets = nearDueTickets,
                UnassignedTickets = unassignedTickets,
                CriticalTickets = criticalTickets,
                ReopenedTickets = reopenedTickets,
                BacklogDelta = backlogDelta,
                ActiveBacklog = activeBacklog,
                AverageResolutionHours = avgResolutionHours,
                AverageFirstResponseHours = avgFirstResponseHours,
                SatisfactionPercent = satisfaction,
                SlaCompliancePercent = slaCompliancePercent,
                CurrentPeriodTickets = currentPeriodTickets,
                PreviousPeriodTickets = previousPeriodTickets,
                CurrentYearTickets = currentYearTickets,
                PreviousYearTickets = previousYearTickets,
                TotalComponentCostCordoba = totalComponentCostCordoba,
                TotalComponentCostUsd = totalComponentCostUsd,
                EstimatedLaborCostCordoba = estimatedLaborCostCordoba,
                EstimatedTotalCostCordoba = estimatedTotalCostCordoba,
                IncidentTypeDistribution = distributionByType,
                TicketsByTechnician = byTechnician,
                TicketsByArea = byArea,
                TicketsBySite = bySite,
                IsFinanceArea = isFinanceArea,
                FinanceTotalRequestedCordoba = financeTotalRequestedCordoba,
                FinanceAverageRequestedCordoba = financeAverageRequestedCordoba,
                FinanceRequestsWithBudgetRisk = financeRequestsWithBudgetRisk,
                FinanceByRequester = financeByRequester,
                FinanceByVendor = financeByVendor,
                FinanceByCostCenter = financeByCostCenter,
                FinanceByStatus = financeByStatus,
                FinanceRequesterPerformance = financeRequesterPerformance,
                TechnicianPerformance = technicianPerformance,
                Alerts = alerts,
                TechnicianOptions = await GetDistinctOptionsAsync(t => t.AssignedTechnician, includeUnassigned: true),
                AreaOptions = await GetDistinctOptionsAsync(t => t.Department),
                SiteOptions = await GetDistinctOptionsAsync(t => t.Site)
            };

            return (model, tickets);
        }

        private async Task<List<ReportSelectOptionViewModel>> GetDistinctOptionsAsync(
            System.Linq.Expressions.Expression<Func<Ticket, string?>> selector,
            bool includeUnassigned = false)
        {
            var values = await _context.Tickets
                .AsNoTracking()
                .Select(selector)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();

            var options = new List<ReportSelectOptionViewModel>
            {
                new() { Value = AllValue, Label = "Todos" }
            };

            if (includeUnassigned)
            {
                options.Add(new ReportSelectOptionViewModel
                {
                    Value = "unassigned",
                    Label = "Sin asignar"
                });
            }

            options.AddRange(values.Select(v => new ReportSelectOptionViewModel
            {
                Value = v,
                Label = v
            }));

            return options;
        }

        private static List<ReportGroupItemViewModel> BuildGroupData(
            IEnumerable<ReportTicketRow> tickets,
            Func<ReportTicketRow, string> keySelector,
            int total)
        {
            return tickets
                .GroupBy(keySelector)
                .Select(g => new ReportGroupItemViewModel
                {
                    Label = g.Key,
                    Count = g.Count(),
                    Percentage = total > 0
                        ? Math.Round((double)g.Count() * 100 / total, 1)
                        : 0
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Label)
                .ToList();
        }

        private static List<ReportGroupItemViewModel> BuildGroupDataFromLabels(
            IEnumerable<string> labels,
            int total)
        {
            return labels
                .GroupBy(label => LabelOrDefault(label, "Sin definir"))
                .Select(g => new ReportGroupItemViewModel
                {
                    Label = g.Key,
                    Count = g.Count(),
                    Percentage = total > 0
                        ? Math.Round((double)g.Count() * 100 / total, 1)
                        : 0
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Label)
                .ToList();
        }

        private static List<ReportAlertViewModel> BuildAlerts(
            int slaCompliancePercent,
            int overdueTickets,
            int nearDueTickets,
            int unassignedTickets,
            int backlogDelta)
        {
            var alerts = new List<ReportAlertViewModel>();

            if (slaCompliancePercent < 90)
            {
                alerts.Add(new ReportAlertViewModel
                {
                    Severity = "danger",
                    Message = $"Cumplimiento SLA bajo: {slaCompliancePercent}% (objetivo >= 90%)."
                });
            }

            if (overdueTickets > 0)
            {
                alerts.Add(new ReportAlertViewModel
                {
                    Severity = "danger",
                    Message = $"Hay {overdueTickets} ticket(s) vencido(s) que requieren accion inmediata."
                });
            }

            if (nearDueTickets > 0)
            {
                alerts.Add(new ReportAlertViewModel
                {
                    Severity = "warning",
                    Message = $"Hay {nearDueTickets} ticket(s) por vencer en ventana critica."
                });
            }

            if (unassignedTickets > 0)
            {
                alerts.Add(new ReportAlertViewModel
                {
                    Severity = "warning",
                    Message = $"Hay {unassignedTickets} ticket(s) sin asignar en el periodo."
                });
            }

            if (backlogDelta > 0)
            {
                alerts.Add(new ReportAlertViewModel
                {
                    Severity = "info",
                    Message = $"El backlog crecio en {backlogDelta} ticket(s) durante el periodo."
                });
            }

            if (alerts.Count == 0)
            {
                alerts.Add(new ReportAlertViewModel
                {
                    Severity = "success",
                    Message = "No se detectan alertas criticas para este periodo."
                });
            }

            return alerts;
        }

        private static bool IsFirstResponseChange(TicketHistory row)
        {
            if (row.FieldChanged.Equals("AssignedTechnician", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return row.FieldChanged.Equals("Status", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(row.NewValue);
        }

        private static bool IsReopenChange(TicketHistory row)
        {
            if (!row.FieldChanged.Equals("Status", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var oldStatus = row.OldValue?.Trim();
            var newStatus = row.NewValue?.Trim();

            var oldClosed = string.Equals(oldStatus, nameof(TicketStatus.Closed), StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(oldStatus, nameof(TicketStatus.Resolved), StringComparison.OrdinalIgnoreCase);

            var reopened = string.Equals(newStatus, nameof(TicketStatus.Open), StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(newStatus, nameof(TicketStatus.InProgress), StringComparison.OrdinalIgnoreCase);

            return oldClosed && reopened;
        }

        private static bool IsSlaCompliant(ReportTicketRow ticket, DateTime utcNow)
        {
            if (ticket.Status is TicketStatus.Closed or TicketStatus.Resolved && ticket.ClosedDate.HasValue)
            {
                return ticket.ClosedDate.Value <= ticket.SLADeadline;
            }

            return utcNow <= ticket.SLADeadline;
        }

        private static string NormalizeTab(string? tab)
        {
            if (string.IsNullOrWhiteSpace(tab))
            {
                return "summary";
            }

            var normalized = tab.Trim().ToLowerInvariant();
            return AllowedTabs.Contains(normalized) ? normalized : "summary";
        }

        private static string NormalizeFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AllValue;
            }

            return value.Trim();
        }

        private static string LabelOrDefault(string? value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value)
                ? defaultValue
                : value.Trim();
        }

        private static FinanceReportMeta ParseFinanceReportMeta(string? description)
        {
            var meta = new FinanceReportMeta();
            if (string.IsNullOrWhiteSpace(description))
            {
                return meta;
            }

            var lines = description
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(x => x.Trim())
                .ToList();

            var amountLine = ReadFinanceValue(lines, "Monto solicitado:");
            if (!string.IsNullOrWhiteSpace(amountLine))
            {
                meta.Currency = amountLine.StartsWith("USD", StringComparison.OrdinalIgnoreCase) ||
                                amountLine.StartsWith("$", StringComparison.OrdinalIgnoreCase)
                    ? "USD"
                    : "C$";
                meta.Amount = ParseFinanceAmount(amountLine);
            }

            meta.CostCenter = ReadFinanceValue(lines, "Centro de costo:");
            meta.VendorName = ReadFinanceValue(lines, "Proveedor o beneficiario:");
            var budgetRisk = ReadFinanceValue(lines, "Riesgo presupuestario:");
            meta.HasBudgetRisk = budgetRisk.Equals("Sobregiro", StringComparison.OrdinalIgnoreCase);

            return meta;
        }

        private static string ReadFinanceValue(List<string> lines, string label)
        {
            var row = lines.FirstOrDefault(x => x.StartsWith(label, StringComparison.OrdinalIgnoreCase));
            if (row == null)
            {
                return string.Empty;
            }

            return row.Substring(label.Length).Trim();
        }

        private static decimal ParseFinanceAmount(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 0m;
            }

            var normalized = raw
                .Replace("USD", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("C$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("$", string.Empty)
                .Replace(",", string.Empty)
                .Trim();

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
                ? value
                : 0m;
        }

        private static decimal ConvertFinanceToCordoba(decimal amount, string? currency)
        {
            if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currency, "$", StringComparison.OrdinalIgnoreCase))
            {
                return decimal.Round(amount * FinanceUsdToCordobaRate, 2, MidpointRounding.AwayFromZero);
            }

            return amount;
        }

        private static string FinanceStatusLabel(TicketStatus status)
        {
            return status switch
            {
                TicketStatus.Open => "Pendiente",
                TicketStatus.InProgress => "En evaluacion",
                TicketStatus.Resolved => "Lista para cierre",
                TicketStatus.Closed => "Cerrada",
                _ => status.ToString()
            };
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private sealed class ReportTicketRow
        {
            public int Id { get; set; }
            public string TicketNumber { get; set; } = string.Empty;
            public string? RequestingUser { get; set; }
            public DateTime CreatedDate { get; set; }
            public DateTime? ClosedDate { get; set; }
            public TicketStatus Status { get; set; }
            public PriorityLevel Priority { get; set; }
            public string? AssignedTechnician { get; set; }
            public string? Department { get; set; }
            public string? Site { get; set; }
            public string? IncidentType { get; set; }
            public DateTime SLADeadline { get; set; }
            public string? Description { get; set; }
            public string? FinanceAccountingEntryNumber { get; set; }
            public string? FinancePurchaseOrderNumber { get; set; }
            public string? FinanceInvoiceNumber { get; set; }
            public DateTime? FinancePaymentDateUtc { get; set; }
            public string? FinanceFinalPaymentMethod { get; set; }
            public string? FinanceReconciliationStatus { get; set; }
            public DateTime? FinanceErpExportedAtUtc { get; set; }
            public string? FinanceErpExportReference { get; set; }
            public bool IsOverdue { get; set; }
            public bool IsNearDue { get; set; }
            public bool IsUnassigned { get; set; }
            public double? FirstResponseHours { get; set; }
            public double? ResolutionHours { get; set; }
            public int ReopenedCount { get; set; }
            public decimal ComponentCostCordoba { get; set; }
            public decimal ComponentCostUsd { get; set; }
        }

        private sealed class FinanceReportMeta
        {
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "C$";
            public string CostCenter { get; set; } = "General";
            public string VendorName { get; set; } = string.Empty;
            public bool HasBudgetRisk { get; set; }
        }
    }
}




