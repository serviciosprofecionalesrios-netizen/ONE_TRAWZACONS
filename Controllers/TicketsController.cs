using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.Services.Interfaces;
using ITServiceDeskApp.ViewModels.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Controllers
{
    [Authorize(Roles = "Administrator,CoordinadorIT,Technician,EndUser,GerenciaGeneral")]
    public class TicketsController : Controller
    {
        private const string QuickAuditPrefix = "QA::";
        private const string SparePartsModelStateKey = "SparePartsJson";
        private const string MaintenanceUiMetaModelStateKey = "maintenanceUiMetaJson";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        private static readonly string[] MaintenanceSites =
        {
            "PCT",
            "PSB",
            "POZ"
        };
        private static readonly string[] MaintenanceTenencias =
        {
            "Propio",
            "Agregado"
        };
        private static readonly string[] MaintenanceFailureCategories =
        {
            "Motor",
            "Transmision",
            "Sistema electrico",
            "Sistema hidraulico",
            "Frenos",
            "Neumaticos",
            "Suspension",
            "Refrigeracion",
            "Carroceria",
            "Otro"
        };
        private static readonly string[] MaintenanceStageOptions = MaintenanceTechnicianCatalog.MaintenanceStageOptions;

        private const decimal MaintenanceCostApprovalThresholdCordoba = 50000m;
        private const decimal MaintenanceCostApprovalThresholdUsd = 1500m;
        private const string MaintenanceAlertSla24HistoryKey = "MaintenanceAlertSla24h";
        private const string MaintenanceAlertSla2HistoryKey = "MaintenanceAlertSla2h";
        private const string MaintenanceAlertOverdueHistoryKey = "MaintenanceAlertOverdue";
        private const string MaintenanceAlertCostApprovalHistoryKey = "MaintenanceAlertCostApproval";
        private const string FinanceApprovalStepHistoryKey = "FinanceApprovalStep";
        private const string FinanceWorkflowHistoryKey = "FinanceWorkflow";
        private const string FinanceRejectionReasonHistoryKey = "FinanceRejectionReason";
        private const string FinanceClosureReasonHistoryKey = "FinanceClosureReason";
        private const string FinanceAlertSla24HistoryKey = "FinanceAlertSla24h";
        private const string FinanceAlertOverdueHistoryKey = "FinanceAlertOverdue";
        private const string FinanceAlertPendingApprovalHistoryKey = "FinancePendingApproval";
        private const string FinanceAlertEscalationHistoryKey = "FinanceEscalation";
        private const string FinanceReturnForCorrectionHistoryKey = "FinanceReturnForCorrection";
        private const string FinanceCorrectionSubmittedHistoryKey = "FinanceCorrectionSubmitted";
        private const string FinanceAccountingTraceHistoryKey = "FinanceAccountingTrace";
        private const decimal FinanceUsdToCordobaRateFallback = 36.5m;
        private const decimal FinanceApprovalTier1Cordoba = 50000m;
        private const decimal FinanceApprovalTier2Cordoba = 150000m;
        private const int FinanceDefaultEscalationHours = 24;
        private static readonly string[] FinanceRequestSites =
        {
            "Plantel San Benito",
            "Mina El Limón",
            "Mina La Libertad",
            "Oficinas Centrales TW",
            "Comprador"
        };
        private static readonly string[] PurchaseRequestSites = { "ACT", "ASB", "AOZ", "ACH", "ALRUS" };
        private static readonly string[] ArticleOutputStatuses = { "PENDIENTE", "EN PROCESO", "ENTREGADO" };
        private static readonly HashSet<string> FinanceHighControlCostCenters = new(StringComparer.OrdinalIgnoreCase)
        {
            "CAPEX",
            "PROYECTOS",
            "INVERSION",
            "INFRAESTRUCTURA MAYOR"
        };
        private static readonly Dictionary<string, decimal> FinanceBudgetByCostCenterCordoba = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Caja Chica"] = 120000m,
            ["Operaciones"] = 350000m,
            ["Administracion"] = 220000m,
            ["IT"] = 180000m,
            ["Mantenimiento"] = 280000m,
            ["Compras"] = 300000m,
            ["General"] = 500000m
        };

        private sealed class MaintenanceSparePartDispatchInput
        {
            public int PartId { get; set; }
            public int Quantity { get; set; }
        }

        private sealed class MaintenanceSparePartCatalogItem
        {
            public int Id { get; set; }
            public string Label { get; set; } = string.Empty;
            public string? Item { get; set; }
            public string? PartNumber { get; set; }
            public string Site { get; set; } = string.Empty;
            public string Tenencia { get; set; } = string.Empty;
            public string UnitOfMeasure { get; set; } = "Unidad";
            public int Stock { get; set; }
            public decimal? UnitCostCordoba { get; set; }
            public decimal? UnitCostUsd { get; set; }
        }

        private sealed class MaintenanceTechnicianCatalogItem
        {
            public string FullName { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Shift { get; set; } = "Diurno";
            public int MaxActiveOrders { get; set; }
            public int CurrentActiveOrders { get; set; }
            public bool IsOverCapacity { get; set; }
            public bool IsAvailable { get; set; }
            public bool IsActive { get; set; }
        }

        private sealed class MaintenanceOrderUiMetaInput
        {
            public string? TemplatePreset { get; set; }
            public decimal? LaborCostCordoba { get; set; }
            public decimal? LaborCostUsd { get; set; }
            public decimal? ExternalCostCordoba { get; set; }
            public decimal? ExternalCostUsd { get; set; }
            public bool ChecklistInspectionCompleted { get; set; }
            public bool ChecklistPartsValidated { get; set; }
            public bool ChecklistFinalTestCompleted { get; set; }
            public bool ChecklistUserConformityCompleted { get; set; }
        }

        private sealed class FinanceTicketMeta
        {
            public string RequestType { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "C$";
            public string CostCenter { get; set; } = "General";
            public DateTime? NeededByDate { get; set; }
            public string VendorName { get; set; } = string.Empty;
            public string ReferenceNumber { get; set; } = string.Empty;
            public string BeneficiaryDocument { get; set; } = string.Empty;
            public string PaymentMethod { get; set; } = string.Empty;
            public string BankAccount { get; set; } = string.Empty;
            public string OcUsage { get; set; } = string.Empty;
            public string OcRequirementJustification { get; set; } = string.Empty;
            public string OcQuantityJustification { get; set; } = string.Empty;
            public string OcStatus { get; set; } = "PENDIENTE";
            public int OcLineCount { get; set; }
            public string OcItemDescription { get; set; } = string.Empty;
            public string OcPurchaseOrderNumber { get; set; } = string.Empty;
            public DateTime? TravelFromDate { get; set; }
            public DateTime? TravelToDate { get; set; }
            public string BusinessJustification { get; set; } = string.Empty;
            public string AdditionalNotes { get; set; } = string.Empty;
            public int RequiredApprovals { get; set; }
        }

        private sealed class FinanceBudgetSnapshot
        {
            public decimal LimitCordoba { get; set; }
            public decimal CommittedCordoba { get; set; }
            public decimal RemainingCordoba { get; set; }
            public bool ExceedsAfterRequest { get; set; }
            public bool HardStopOnOverrun { get; set; } = true;
            public bool RequireAuthorizationOnOverrun { get; set; } = true;
            public int AlertThresholdPercent { get; set; } = 80;
            public string CostCenterNumber { get; set; } = "General";
            public string CostCenterName { get; set; } = "General";
            public string Area { get; set; } = string.Empty;
            public decimal RequestedCordoba { get; set; }
            public double UsagePercentAfterRequest { get; set; }
            public bool IsNearThresholdAfterRequest { get; set; }
            public int Year { get; set; }
            public int Month { get; set; }
        }

        private sealed class FinanceApprovalPolicy
        {
            public int RequiredApprovals { get; set; }
            public int EscalationHours { get; set; }
            public int? RuleId { get; set; }
        }

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly IWhatsAppNotificationService _whatsAppNotificationService;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IEmailNotificationService emailNotificationService,
            IWhatsAppNotificationService whatsAppNotificationService,
            ILogger<TicketsController> logger)
        {
            _context = context;
            _environment = environment;
            _emailNotificationService = emailNotificationService;
            _whatsAppNotificationService = whatsAppNotificationService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(
            string? search,
            TicketStatus? status,
            PriorityLevel? priority,
            string? quick,
            string? department,
            string? site,
            string? tenencia,
            string? technician,
            string? incidentType,
            string? maintenanceStage,
            string? overdueBand,
            string? maintenanceView,
            bool mine = false,
            int page = 1,
            int pageSize = 25)
        {
            var nowUtc = DateTime.UtcNow;
            var nearThresholdUtc = nowUtc.AddHours(4);
            var currentUser = User.Identity?.Name?.Trim();
            var normalizedQuick = NormalizeQuickFilter(quick);
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var normalizedDepartment = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            var normalizedSite = string.IsNullOrWhiteSpace(site) ? null : site.Trim();
            var normalizedTenencia = string.IsNullOrWhiteSpace(tenencia) ? null : tenencia.Trim();
            var normalizedAssignedTechnician = string.IsNullOrWhiteSpace(technician) ? null : technician.Trim();
            var normalizedIncidentType = string.IsNullOrWhiteSpace(incidentType) ? null : incidentType.Trim();
            var normalizedMaintenanceStage = string.IsNullOrWhiteSpace(maintenanceStage) ? null : maintenanceStage.Trim();
            var normalizedOverdueBand = NormalizeOverdueBand(overdueBand);
            var normalizedMaintenanceView = NormalizeMaintenanceViewMode(maintenanceView);
            var isMaintenanceView = normalizedDepartment != null &&
                                    normalizedDepartment.Equals("Mantenimiento", StringComparison.OrdinalIgnoreCase);
            var isFinanceView = normalizedDepartment != null &&
                                normalizedDepartment.Equals("Finanzas", StringComparison.OrdinalIgnoreCase);
            var isMineFinanceView = isFinanceView && mine;
            var normalizedPageSize = pageSize switch
            {
                <= 10 => 10,
                <= 25 => 25,
                <= 50 => 50,
                _ => 100
            };
            var todayLocal = ToBusinessLocalTime(nowUtc).Date;
            var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal, SlaBusinessTimeZone);
            var tomorrowStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal.AddDays(1), SlaBusinessTimeZone);

            IQueryable<Ticket> query = _context.Tickets.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(t =>
                    (t.TicketNumber != null && t.TicketNumber.Contains(normalizedSearch)) ||
                    t.RequestingUser.Contains(normalizedSearch) ||
                    t.Description.Contains(normalizedSearch));
            }

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (priority.HasValue)
            {
                query = query.Where(t => t.Priority == priority.Value);
            }

            if (!string.IsNullOrWhiteSpace(normalizedDepartment))
            {
                query = query.Where(t => t.Department == normalizedDepartment);
            }

            if (!string.IsNullOrWhiteSpace(normalizedSite))
            {
                query = query.Where(t => t.Site == normalizedSite);
            }

            if (!string.IsNullOrWhiteSpace(normalizedTenencia))
            {
                query = query.Where(t => t.Tenencia == normalizedTenencia);
            }

            if (!string.IsNullOrWhiteSpace(normalizedAssignedTechnician))
            {
                query = query.Where(t => t.AssignedTechnician == normalizedAssignedTechnician);
            }

            if (!string.IsNullOrWhiteSpace(normalizedIncidentType))
            {
                query = query.Where(t => t.IncidentType == normalizedIncidentType);
            }

            if (!string.IsNullOrWhiteSpace(normalizedMaintenanceStage))
            {
                query = query.Where(t => t.MaintenanceStage == normalizedMaintenanceStage);
            }

            if (isMineFinanceView && !string.IsNullOrWhiteSpace(currentUser))
            {
                var normalizedCurrentUser = currentUser.Trim();
                query = query.Where(t =>
                    t.RequestingUser != null &&
                    t.RequestingUser.ToUpper() == normalizedCurrentUser.ToUpper());
            }

            var financeOpenCount = 0;
            var financeInProgressCount = 0;
            var financeResolvedCount = 0;
            var financeClosedCount = 0;
            var financePendingApprovalCount = 0;
            var financeRejectedCount = 0;
            var financeReturnedCount = 0;
            var financeEscalatedCount = 0;
            var financeOverBudgetRequests = 0;
            var financeBudgetLimitCordoba = 0m;
            var financeBudgetCommittedCordoba = 0m;
            var financeBudgetUsagePercent = 0d;
            var financeApprovedAmountCurrentMonthCordoba = 0m;
            var financeAverageCycleHours = 0d;

            if (isFinanceView)
            {
                var financeUsdToCordobaRate = await GetFinanceUsdToCordobaRateAsync();
                var financeDashboardQuery = _context.Tickets.AsNoTracking().Where(t =>
                    t.Department != null &&
                    t.Department == "Finanzas");

                if (isMineFinanceView && !string.IsNullOrWhiteSpace(currentUser))
                {
                    var normalizedCurrentUser = currentUser.Trim();
                    financeDashboardQuery = financeDashboardQuery.Where(t =>
                        t.RequestingUser != null &&
                        t.RequestingUser.ToUpper() == normalizedCurrentUser.ToUpper());
                }

                var financeRows = await financeDashboardQuery
                    .Select(t => new
                    {
                        t.Id,
                        t.CreatedDate,
                        t.Status,
                        t.CostApproved,
                        t.Description,
                        t.SLADeadline,
                        t.TicketNumber,
                        t.FinanceReturnedForCorrection,
                        t.FinanceEscalatedAtUtc,
                        t.ClosedDate
                    })
                    .ToListAsync();

                financeOpenCount = financeRows.Count(t => t.Status == TicketStatus.Open);
                financeInProgressCount = financeRows.Count(t => t.Status == TicketStatus.InProgress);
                financeResolvedCount = financeRows.Count(t => t.Status == TicketStatus.Resolved);
                financeClosedCount = financeRows.Count(t => t.Status == TicketStatus.Closed);
                financePendingApprovalCount = financeRows.Count(t =>
                    (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress) &&
                    !t.CostApproved);
                financeRejectedCount = financeRows.Count(t => t.Status == TicketStatus.Closed && !t.CostApproved);
                financeReturnedCount = financeRows.Count(t => t.FinanceReturnedForCorrection && t.Status != TicketStatus.Closed);
                financeEscalatedCount = financeRows.Count(t => t.FinanceEscalatedAtUtc.HasValue && t.Status != TicketStatus.Closed);

                var closedCycleRows = financeRows
                    .Where(t => t.ClosedDate.HasValue)
                    .ToList();
                if (closedCycleRows.Count > 0)
                {
                    financeAverageCycleHours = Math.Round(
                        closedCycleRows.Average(t => (t.ClosedDate!.Value - t.CreatedDate).TotalHours),
                        1);
                }

                var financeAlertScope = financeRows
                    .Where(t => t.Status != TicketStatus.Closed)
                    .Select(t => new Ticket
                    {
                        Id = t.Id,
                        TicketNumber = t.TicketNumber,
                        Status = t.Status,
                        CostApproved = t.CostApproved,
                        SLADeadline = t.SLADeadline
                    })
                    .ToList();
                await EnsureFinanceAlertsAsync(financeAlertScope, nowUtc);

                var groupedByCenter = financeRows
                    .Select(row => new
                    {
                        row.CreatedDate,
                        row.Status,
                        row.CostApproved,
                        Meta = ParseFinanceTicketMeta(row.Description)
                    })
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.Meta.CostCenter) ? "General" : x.Meta.CostCenter.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var activeCostCenterCatalog = await GetActiveFinanceCostCentersAsync();

                foreach (var centerGroup in groupedByCenter)
                {
                    var centerName = centerGroup.Key;
                    var configuredCenter = FindFinanceCostCenter(activeCostCenterCatalog, centerName);
                    var centerLimit = configuredCenter != null
                        ? configuredCenter.MonthlyBudgetCordoba
                        : FinanceBudgetByCostCenterCordoba.TryGetValue(centerName, out var configuredCenterLimit)
                            ? configuredCenterLimit
                            : FinanceBudgetByCostCenterCordoba["General"];
                    financeBudgetLimitCordoba += centerLimit;

                    var runningCommitted = 0m;
                    foreach (var row in centerGroup.OrderBy(x => x.CreatedDate))
                    {
                        if (row.Status == TicketStatus.Closed && !row.CostApproved)
                        {
                            continue;
                        }

                        var amountCordoba = ConvertToCordoba(row.Meta.Amount, row.Meta.Currency, financeUsdToCordobaRate);
                        if (runningCommitted + amountCordoba > centerLimit)
                        {
                            financeOverBudgetRequests++;
                        }

                        runningCommitted += amountCordoba;
                    }

                    financeBudgetCommittedCordoba += runningCommitted;
                }

                financeApprovedAmountCurrentMonthCordoba = financeRows
                    .Where(x =>
                        x.CostApproved &&
                        x.CreatedDate.Year == nowUtc.Year &&
                        x.CreatedDate.Month == nowUtc.Month)
                    .Sum(x =>
                    {
                        var meta = ParseFinanceTicketMeta(x.Description);
                        return ConvertToCordoba(meta.Amount, meta.Currency, financeUsdToCordobaRate);
                    });

                financeBudgetUsagePercent = financeBudgetLimitCordoba > 0
                    ? Math.Round((double)(financeBudgetCommittedCordoba / financeBudgetLimitCordoba) * 100d, 1)
                    : 0d;
            }

            query = ApplyQuickFilter(
                query,
                normalizedQuick,
                nowUtc,
                todayStartUtc,
                tomorrowStartUtc,
                currentUser);

            var overdueBand0To24Count = 0;
            var overdueBand24To72Count = 0;
            var overdueBandOver72Count = 0;

            if (isMaintenanceView)
            {
                var overdueScope = query.Where(t => t.Status != TicketStatus.Closed && t.SLADeadline < nowUtc);
                var threshold24Utc = nowUtc.AddHours(-24);
                var threshold72Utc = nowUtc.AddHours(-72);

                overdueBand0To24Count = await overdueScope.CountAsync(t => t.SLADeadline >= threshold24Utc);
                overdueBand24To72Count = await overdueScope.CountAsync(t => t.SLADeadline < threshold24Utc && t.SLADeadline >= threshold72Utc);
                overdueBandOver72Count = await overdueScope.CountAsync(t => t.SLADeadline < threshold72Utc);

                query = ApplyOverdueBandFilter(query, normalizedOverdueBand, nowUtc);
            }

            if (isMaintenanceView)
            {
                var alertScope = await query
                    .Where(t => t.Status != TicketStatus.Closed)
                    .Select(t => new Ticket
                    {
                        Id = t.Id,
                        TicketNumber = t.TicketNumber,
                        CreatedDate = t.CreatedDate,
                        RequestingUser = t.RequestingUser,
                        Site = t.Site,
                        UnitCode = t.UnitCode,
                        IncidentType = t.IncidentType,
                        Priority = t.Priority,
                        Status = t.Status,
                        AssignedTechnician = t.AssignedTechnician,
                        MaintenanceStage = t.MaintenanceStage,
                        SLADeadline = t.SLADeadline,
                        Description = t.Description,
                        ChangedComponentCostCordoba = t.ChangedComponentCostCordoba,
                        ChangedComponentCostUsd = t.ChangedComponentCostUsd,
                        LaborCostCordoba = t.LaborCostCordoba,
                        LaborCostUsd = t.LaborCostUsd,
                        ExternalCostCordoba = t.ExternalCostCordoba,
                        ExternalCostUsd = t.ExternalCostUsd,
                        RequiresCostApproval = t.RequiresCostApproval,
                        CostApproved = t.CostApproved,
                        CostApprovedBy = t.CostApprovedBy
                    })
                    .ToListAsync();

                await EnsureMaintenanceAlertsAsync(alertScope, nowUtc);
            }

            var totalItems = await query.CountAsync();
            var activeTickets = await query.CountAsync(t => t.Status != TicketStatus.Closed);
            var overdueTickets = await query.CountAsync(t => t.Status != TicketStatus.Closed && t.SLADeadline < nowUtc);
            var nearDueTickets = await query.CountAsync(t =>
                t.Status != TicketStatus.Closed &&
                t.SLADeadline >= nowUtc &&
                t.SLADeadline < nearThresholdUtc);
            var unassignedActiveTickets = await query.CountAsync(t =>
                t.Status != TicketStatus.Closed &&
                (t.AssignedTechnician == null || t.AssignedTechnician == ""));
            var criticalActiveTickets = await query.CountAsync(t =>
                t.Status != TicketStatus.Closed &&
                t.Priority == PriorityLevel.Critical);

            var slaCompliant = await query
                .Where(t => t.Status != TicketStatus.Closed)
                .CountAsync(t => t.SLADeadline >= nearThresholdUtc);

            var slaComplianceRate = activeTickets == 0
                ? 0
                : Math.Round((double)slaCompliant / activeTickets * 100, 1);

            var trendStartLocal = todayLocal.AddDays(-6);
            var trendStartUtc = TimeZoneInfo.ConvertTimeToUtc(trendStartLocal, SlaBusinessTimeZone);
            var trendEndUtc = tomorrowStartUtc;

            var createdTrendUtc = await query
                .Where(t => t.CreatedDate >= trendStartUtc && t.CreatedDate < trendEndUtc)
                .Select(t => t.CreatedDate)
                .ToListAsync();

            var closedTrendUtc = await query
                .Where(t => t.ClosedDate.HasValue && t.ClosedDate.Value >= trendStartUtc && t.ClosedDate.Value < trendEndUtc)
                .Select(t => t.ClosedDate!.Value)
                .ToListAsync();

            var trendDaysLocal = Enumerable.Range(0, 7)
                .Select(offset => trendStartLocal.AddDays(offset))
                .ToList();

            var trendLabels = trendDaysLocal
                .Select(d => d.ToString("dd/MM"))
                .ToList();

            var trendCreated = trendDaysLocal
                .Select(localDay => createdTrendUtc.Count(createdAt => ToBusinessLocalTime(createdAt).Date == localDay))
                .ToList();

            var trendClosed = trendDaysLocal
                .Select(localDay => closedTrendUtc.Count(closedAt => ToBusinessLocalTime(closedAt).Date == localDay))
                .ToList();

            var createdLast7Days = trendCreated.Sum();
            var closedLast7Days = trendClosed.Sum();
            var preventiveOrders = 0;
            var correctiveOrders = 0;
            var pendingSparePartOrders = 0;
            var componentChangeOrders = 0;
            var closedTodayOrders = 0;
            var mttrHours = 0d;
            var pendingCostApprovalOrders = 0;
            var totalMaintenanceCostCordoba = 0m;
            var totalMaintenanceCostUsd = 0m;
            var averageMaintenanceCostCordoba = 0m;
            var averageMaintenanceCostUsd = 0m;
            var ordersWithTrackedCost = 0;
            var inventoryLowStockCount = 0;
            var inventoryOutOfStockCount = 0;
            var inventoryInTransitUnits = 0;
            var inventoryHealthTone = "success";
            var inventoryHealthLabel = "Stock estable";
            var maintenanceSiteCostRanking = new List<MaintenanceSiteCostRankItem>();
            var maintenanceReincidenceRanking = new List<MaintenanceReincidenceItem>();
            var maintenanceDetailRows = new List<MaintenanceOrderDetailItem>();
            var maintenanceBoardRows = new List<MaintenanceAirportBoardItem>();
            var stageKpis = MaintenanceStageOptions
                .Select(stage => new MaintenanceStageKpiItem
                {
                    Stage = stage,
                    Total = 0
                })
                .ToList();
            var technicianCapacityItems = new List<TechnicianCapacityKpiItem>();
            var technicianCapacityUtilizationRate = 0d;

            if (isMaintenanceView)
            {
                preventiveOrders = await query.CountAsync(t =>
                    t.IncidentType != null &&
                    EF.Functions.Like(t.IncidentType, "%Preventiv%"));

                correctiveOrders = await query.CountAsync(t =>
                    t.IncidentType != null &&
                    (EF.Functions.Like(t.IncidentType, "%Correctiv%") ||
                     EF.Functions.Like(t.IncidentType, "%Reparac%")));

                pendingSparePartOrders = await query.CountAsync(t =>
                    t.Status != TicketStatus.Closed &&
                    t.SparePartRequired &&
                    !t.SparePartPurchased);

                componentChangeOrders = await query.CountAsync(t => t.ComponentChanged);

                closedTodayOrders = await query.CountAsync(t =>
                    t.Status == TicketStatus.Closed &&
                    t.ClosedDate.HasValue &&
                    t.ClosedDate.Value >= todayStartUtc &&
                    t.ClosedDate.Value < tomorrowStartUtc);

                pendingCostApprovalOrders = await query.CountAsync(t =>
                    t.Status != TicketStatus.Closed &&
                    t.RequiresCostApproval &&
                    !t.CostApproved);

                var closedDurations = await query
                    .Where(t => t.Status == TicketStatus.Closed && t.ClosedDate.HasValue)
                    .Select(t => new
                    {
                        t.CreatedDate,
                        ClosedDate = t.ClosedDate!.Value
                    })
                    .ToListAsync();

                if (closedDurations.Count > 0)
                {
                    mttrHours = Math.Round(
                        closedDurations.Average(x => Math.Max(0d, (x.ClosedDate - x.CreatedDate).TotalHours)),
                        1);
                }

                var filteredTicketIdsQuery = query.Select(t => t.Id);

                var dispatchCostTotals = await _context.TicketSparePartDispatches
                    .AsNoTracking()
                    .Where(d => filteredTicketIdsQuery.Contains(d.TicketId))
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        TotalCordoba = g.Sum(x => x.TotalCostCordoba ?? 0m),
                        TotalUsd = g.Sum(x => x.TotalCostUsd ?? 0m)
                    })
                    .FirstOrDefaultAsync();

                var componentCostCordoba = await query.SumAsync(t => t.ChangedComponentCostCordoba ?? 0m);
                var componentCostUsd = await query.SumAsync(t => t.ChangedComponentCostUsd ?? 0m);
                var laborCostCordoba = await query.SumAsync(t => t.LaborCostCordoba ?? 0m);
                var laborCostUsd = await query.SumAsync(t => t.LaborCostUsd ?? 0m);
                var externalCostCordoba = await query.SumAsync(t => t.ExternalCostCordoba ?? 0m);
                var externalCostUsd = await query.SumAsync(t => t.ExternalCostUsd ?? 0m);

                totalMaintenanceCostCordoba = (dispatchCostTotals?.TotalCordoba ?? 0m) + componentCostCordoba + laborCostCordoba + externalCostCordoba;
                totalMaintenanceCostUsd = (dispatchCostTotals?.TotalUsd ?? 0m) + componentCostUsd + laborCostUsd + externalCostUsd;

                var ticketIdsWithDispatch = _context.TicketSparePartDispatches
                    .AsNoTracking()
                    .Where(d => filteredTicketIdsQuery.Contains(d.TicketId))
                    .Select(d => d.TicketId)
                    .Distinct();

                var ticketIdsWithComponentCost = query
                    .Where(t =>
                        (t.ChangedComponentCostCordoba.HasValue && t.ChangedComponentCostCordoba.Value > 0) ||
                        (t.ChangedComponentCostUsd.HasValue && t.ChangedComponentCostUsd.Value > 0) ||
                        (t.LaborCostCordoba.HasValue && t.LaborCostCordoba.Value > 0) ||
                        (t.LaborCostUsd.HasValue && t.LaborCostUsd.Value > 0) ||
                        (t.ExternalCostCordoba.HasValue && t.ExternalCostCordoba.Value > 0) ||
                        (t.ExternalCostUsd.HasValue && t.ExternalCostUsd.Value > 0))
                    .Select(t => t.Id);

                ordersWithTrackedCost = await ticketIdsWithDispatch
                    .Union(ticketIdsWithComponentCost)
                    .CountAsync();

                if (ordersWithTrackedCost > 0)
                {
                    averageMaintenanceCostCordoba = decimal.Round(
                        totalMaintenanceCostCordoba / ordersWithTrackedCost,
                        2,
                        MidpointRounding.AwayFromZero);
                    averageMaintenanceCostUsd = decimal.Round(
                        totalMaintenanceCostUsd / ordersWithTrackedCost,
                        2,
                        MidpointRounding.AwayFromZero);
                }

                var stageCounts = await query
                    .Where(t => !string.IsNullOrWhiteSpace(t.MaintenanceStage))
                    .GroupBy(t => t.MaintenanceStage!)
                    .Select(g => new
                    {
                        Stage = g.Key,
                        Total = g.Count()
                    })
                    .ToListAsync();

                stageKpis = MaintenanceStageOptions
                    .Select(stage => new MaintenanceStageKpiItem
                    {
                        Stage = stage,
                        Total = stageCounts
                            .Where(x => x.Stage.Equals(stage, StringComparison.OrdinalIgnoreCase))
                            .Select(x => x.Total)
                            .FirstOrDefault()
                    })
                    .ToList();
            }

            var technicianLoads = await query
                .Where(t => t.Status != TicketStatus.Closed)
                .GroupBy(t => t.AssignedTechnician == null || t.AssignedTechnician == "" ? "Sin asignar" : t.AssignedTechnician)
                .Select(g => new TechnicianLoadItem
                {
                    Technician = g.Key,
                    ActiveTickets = g.Count(),
                    OverdueTickets = g.Count(t => t.SLADeadline < nowUtc),
                    NearDueTickets = g.Count(t => t.SLADeadline >= nowUtc && t.SLADeadline < nearThresholdUtc),
                    OnTimeTickets = g.Count(t => t.SLADeadline >= nearThresholdUtc),
                    CriticalTickets = g.Count(t => t.Priority == PriorityLevel.Critical)
                })
                .OrderByDescending(x => x.ActiveTickets)
                .ThenByDescending(x => x.OverdueTickets)
                .Take(6)
                .ToListAsync();

            List<string> technicianOptions;
            if (isMaintenanceView)
            {
                technicianOptions = await _context.MaintenanceTechnicians
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .Select(x => x.FullName.Trim())
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();

                var activeLoads = await query
                    .Where(t => t.Status != TicketStatus.Closed && !string.IsNullOrWhiteSpace(t.AssignedTechnician))
                    .GroupBy(t => t.AssignedTechnician!)
                    .Select(g => new
                    {
                        Technician = g.Key,
                        ActiveCount = g.Count()
                    })
                    .ToListAsync();

                var loadByTechnician = activeLoads.ToDictionary(
                    x => x.Technician,
                    x => x.ActiveCount,
                    StringComparer.OrdinalIgnoreCase);

                var capacityRows = await _context.MaintenanceTechnicians
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Category)
                    .ThenBy(x => x.FullName)
                    .ToListAsync();

                technicianCapacityItems = capacityRows
                    .Select(x =>
                    {
                        var activeCount = loadByTechnician.TryGetValue(x.FullName, out var value) ? value : 0;
                        var capacity = Math.Max(1, x.MaxActiveOrders);
                        var utilization = Math.Round(activeCount * 100d / capacity, 1);

                        return new TechnicianCapacityKpiItem
                        {
                            Technician = x.FullName,
                            Category = x.Category,
                            Shift = x.Shift,
                            ActiveOrders = activeCount,
                            Capacity = capacity,
                            UtilizationRate = utilization,
                            IsAvailable = x.IsAvailable,
                            IsOverCapacity = activeCount >= capacity
                        };
                    })
                    .ToList();

                if (technicianCapacityItems.Count > 0)
                {
                    technicianCapacityUtilizationRate = Math.Round(
                        technicianCapacityItems.Average(x => x.UtilizationRate),
                        1);
                }
            }
            else
            {
                technicianOptions = await _context.Users
                    .AsNoTracking()
                    .Where(u =>
                        u.IsActive &&
                        (u.Role == UserRole.Technician ||
                         u.Role == UserRole.CoordinadorIT ||
                         u.Role == UserRole.Administrator))
                    .Select(u => u.FullName)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();
            }

            var siteOptions = new List<string>();
            var tenenciaOptions = new List<string>();
            var incidentTypeOptions = new List<string>();

            if (isMaintenanceView)
            {
                siteOptions = MaintenanceSites.ToList();

                if (!string.IsNullOrWhiteSpace(normalizedSite) &&
                    siteOptions.All(s => !s.Equals(normalizedSite, StringComparison.OrdinalIgnoreCase)))
                {
                    siteOptions.Insert(0, normalizedSite);
                }

                tenenciaOptions = MaintenanceTenencias.ToList();

                if (!string.IsNullOrWhiteSpace(normalizedTenencia) &&
                    tenenciaOptions.All(t => !t.Equals(normalizedTenencia, StringComparison.OrdinalIgnoreCase)))
                {
                    tenenciaOptions.Insert(0, normalizedTenencia);
                }

                incidentTypeOptions = await _context.Tickets
                    .AsNoTracking()
                    .Where(t =>
                        t.Department == "Mantenimiento" &&
                        !string.IsNullOrWhiteSpace(t.IncidentType))
                    .Select(t => t.IncidentType.Trim())
                    .Distinct()
                    .OrderBy(t => t)
                    .ToListAsync();

                if (!string.IsNullOrWhiteSpace(normalizedIncidentType) &&
                    incidentTypeOptions.All(t => !t.Equals(normalizedIncidentType, StringComparison.OrdinalIgnoreCase)))
                {
                    incidentTypeOptions.Insert(0, normalizedIncidentType);
                }

                if (!string.IsNullOrWhiteSpace(normalizedAssignedTechnician) &&
                    technicianOptions.All(t => !t.Equals(normalizedAssignedTechnician, StringComparison.OrdinalIgnoreCase)))
                {
                    technicianOptions.Insert(0, normalizedAssignedTechnician);
                }

                if (!string.IsNullOrWhiteSpace(normalizedMaintenanceStage) &&
                    MaintenanceStageOptions.All(x => !x.Equals(normalizedMaintenanceStage, StringComparison.OrdinalIgnoreCase)))
                {
                    normalizedMaintenanceStage = null;
                }

                var inventoryQuery = _context.MaintenanceInventoryParts
                    .AsNoTracking()
                    .Where(x => x.IsActive);

                if (!string.IsNullOrWhiteSpace(normalizedSite))
                {
                    inventoryQuery = inventoryQuery.Where(x => x.Site == normalizedSite);
                }

                if (!string.IsNullOrWhiteSpace(normalizedTenencia))
                {
                    inventoryQuery = inventoryQuery.Where(x => x.Tenencia == normalizedTenencia);
                }

                inventoryLowStockCount = await inventoryQuery
                    .CountAsync(x => x.QuantityOnHand > 0 && x.QuantityOnHand <= x.MinimumStock);

                inventoryOutOfStockCount = await inventoryQuery
                    .CountAsync(x => x.QuantityOnHand <= 0);

                inventoryInTransitUnits = await inventoryQuery
                    .Select(x => (int?)x.QuantityInTransit)
                    .SumAsync() ?? 0;

                if (inventoryOutOfStockCount > 0)
                {
                    inventoryHealthTone = "danger";
                    inventoryHealthLabel = $"{inventoryOutOfStockCount} agotado(s)";
                }
                else if (inventoryLowStockCount > 0)
                {
                    inventoryHealthTone = "warning";
                    inventoryHealthLabel = $"{inventoryLowStockCount} con bajo stock";
                }

                var maintenanceTicketRows = await query
                    .Select(t => new
                    {
                        t.Id,
                        t.Site,
                        t.UnitCode,
                        t.FailureCategory,
                        ComponentCostCordoba = t.ChangedComponentCostCordoba ?? 0m,
                        ComponentCostUsd = t.ChangedComponentCostUsd ?? 0m,
                        LaborCostCordoba = t.LaborCostCordoba ?? 0m,
                        LaborCostUsd = t.LaborCostUsd ?? 0m,
                        ExternalCostCordoba = t.ExternalCostCordoba ?? 0m,
                        ExternalCostUsd = t.ExternalCostUsd ?? 0m
                    })
                    .ToListAsync();

                var maintenanceTicketIds = maintenanceTicketRows
                    .Select(x => x.Id)
                    .Distinct()
                    .ToList();

                var dispatchTotalsByTicket = new Dictionary<int, (decimal TotalCordoba, decimal TotalUsd)>();
                if (maintenanceTicketIds.Count > 0)
                {
                    var dispatchTotals = await _context.TicketSparePartDispatches
                        .AsNoTracking()
                        .Where(d => maintenanceTicketIds.Contains(d.TicketId))
                        .GroupBy(d => d.TicketId)
                        .Select(g => new
                        {
                            TicketId = g.Key,
                            TotalCordoba = g.Sum(x => x.TotalCostCordoba ?? 0m),
                            TotalUsd = g.Sum(x => x.TotalCostUsd ?? 0m)
                        })
                        .ToListAsync();

                    dispatchTotalsByTicket = dispatchTotals.ToDictionary(
                        x => x.TicketId,
                        x => (TotalCordoba: x.TotalCordoba, TotalUsd: x.TotalUsd));
                }

                maintenanceSiteCostRanking = maintenanceTicketRows
                    .Select(row =>
                    {
                        var normalizedSiteName = string.IsNullOrWhiteSpace(row.Site)
                            ? "Sin sitio"
                            : row.Site.Trim();

                        var dispatchCost = dispatchTotalsByTicket.TryGetValue(row.Id, out var totals)
                            ? totals
                            : (TotalCordoba: 0m, TotalUsd: 0m);

                        return new
                        {
                            Site = normalizedSiteName,
                            TotalCostCordoba = dispatchCost.TotalCordoba + row.ComponentCostCordoba + row.LaborCostCordoba + row.ExternalCostCordoba,
                            TotalCostUsd = dispatchCost.TotalUsd + row.ComponentCostUsd + row.LaborCostUsd + row.ExternalCostUsd,
                            UnitCode = string.IsNullOrWhiteSpace(row.UnitCode) ? null : row.UnitCode.Trim(),
                            FailureCategory = string.IsNullOrWhiteSpace(row.FailureCategory) ? null : row.FailureCategory.Trim()
                        };
                    })
                    .GroupBy(x => x.Site)
                    .Select(g =>
                    {
                        var orders = g.Count();
                        var totalCostCordoba = g.Sum(x => x.TotalCostCordoba);
                        var totalCostUsd = g.Sum(x => x.TotalCostUsd);

                        var recurrentOrderCount = g
                            .Where(x => !string.IsNullOrWhiteSpace(x.UnitCode))
                            .GroupBy(x => new
                            {
                                UnitCode = x.UnitCode!.ToUpperInvariant(),
                                FailureCategory = string.IsNullOrWhiteSpace(x.FailureCategory)
                                    ? "SIN-CATEGORIA"
                                    : x.FailureCategory!.ToUpperInvariant()
                            })
                            .Where(rg => rg.Count() > 1)
                            .Sum(rg => rg.Count() - 1);

                        return new MaintenanceSiteCostRankItem
                        {
                            Site = g.Key,
                            Orders = orders,
                            TotalCostCordoba = totalCostCordoba,
                            TotalCostUsd = totalCostUsd,
                            AverageCostCordoba = orders > 0
                                ? decimal.Round(totalCostCordoba / orders, 2, MidpointRounding.AwayFromZero)
                                : 0m,
                            AverageCostUsd = orders > 0
                                ? decimal.Round(totalCostUsd / orders, 2, MidpointRounding.AwayFromZero)
                                : 0m,
                            ReincidenceOrders = recurrentOrderCount
                        };
                    })
                    .OrderByDescending(x => x.TotalCostCordoba)
                    .ThenByDescending(x => x.TotalCostUsd)
                    .Take(6)
                    .ToList();

                maintenanceReincidenceRanking = maintenanceTicketRows
                    .Where(x => !string.IsNullOrWhiteSpace(x.UnitCode))
                    .GroupBy(x => x.UnitCode!.Trim().ToUpperInvariant())
                    .Where(g => g.Count() > 1)
                    .Select(g =>
                    {
                        var topFailureCategory = g
                            .Where(x => !string.IsNullOrWhiteSpace(x.FailureCategory))
                            .GroupBy(x => x.FailureCategory!.Trim())
                            .OrderByDescending(rg => rg.Count())
                            .Select(rg => rg.Key)
                            .FirstOrDefault() ?? "Sin categorÃ­a";

                        var mainSite = g
                            .Where(x => !string.IsNullOrWhiteSpace(x.Site))
                            .GroupBy(x => x.Site!.Trim())
                            .OrderByDescending(rg => rg.Count())
                            .Select(rg => rg.Key)
                            .FirstOrDefault() ?? "Sin sitio";

                        return new MaintenanceReincidenceItem
                        {
                            UnitCode = g.Key,
                            Orders = g.Count(),
                            ReincidenceCount = g.Count() - 1,
                            TopFailureCategory = topFailureCategory,
                            MainSite = mainSite
                        };
                    })
                    .OrderByDescending(x => x.ReincidenceCount)
                    .ThenByDescending(x => x.Orders)
                    .Take(8)
                    .ToList();

                var detailSourceRows = await query
                    .Select(t => new
                    {
                        t.Id,
                        t.TicketNumber,
                        t.CreatedDate,
                        t.SLADeadline,
                        t.Site,
                        t.UnitCode,
                        t.IncidentType,
                        t.FailureCategory,
                        t.MaintenanceStage,
                        t.AssignedTechnician,
                        t.Status,
                        t.RequiresCostApproval,
                        t.CostApproved,
                        ComponentCostCordoba = t.ChangedComponentCostCordoba ?? 0m,
                        ComponentCostUsd = t.ChangedComponentCostUsd ?? 0m,
                        LaborCostCordoba = t.LaborCostCordoba ?? 0m,
                        LaborCostUsd = t.LaborCostUsd ?? 0m,
                        ExternalCostCordoba = t.ExternalCostCordoba ?? 0m,
                        ExternalCostUsd = t.ExternalCostUsd ?? 0m
                    })
                    .OrderBy(t => t.Status == TicketStatus.Closed ? 1 : 0)
                    .ThenBy(t => t.SLADeadline)
                    .ThenByDescending(t => t.CreatedDate)
                    .Take(300)
                    .ToListAsync();

                var detailTicketIds = detailSourceRows
                    .Select(x => x.Id)
                    .Distinct()
                    .ToList();

                var dispatchTotalsForDetail = new Dictionary<int, (decimal TotalCordoba, decimal TotalUsd)>();
                if (detailTicketIds.Count > 0)
                {
                    var dispatchRows = await _context.TicketSparePartDispatches
                        .AsNoTracking()
                        .Where(d => detailTicketIds.Contains(d.TicketId))
                        .GroupBy(d => d.TicketId)
                        .Select(g => new
                        {
                            TicketId = g.Key,
                            TotalCordoba = g.Sum(x => x.TotalCostCordoba ?? 0m),
                            TotalUsd = g.Sum(x => x.TotalCostUsd ?? 0m)
                        })
                        .ToListAsync();

                    dispatchTotalsForDetail = dispatchRows.ToDictionary(
                        x => x.TicketId,
                        x => (TotalCordoba: x.TotalCordoba, TotalUsd: x.TotalUsd));
                }

                maintenanceDetailRows = detailSourceRows
                    .Select(row =>
                    {
                        var dispatchCost = dispatchTotalsForDetail.TryGetValue(row.Id, out var totals)
                            ? totals
                            : (TotalCordoba: 0m, TotalUsd: 0m);

                        return new MaintenanceOrderDetailItem
                        {
                            TicketId = row.Id,
                            TicketNumber = string.IsNullOrWhiteSpace(row.TicketNumber) ? $"TK-{row.Id}" : row.TicketNumber!,
                            CreatedDateUtc = row.CreatedDate,
                            SlaDeadlineUtc = row.SLADeadline,
                            Site = string.IsNullOrWhiteSpace(row.Site) ? "-" : row.Site,
                            UnitCode = string.IsNullOrWhiteSpace(row.UnitCode) ? "Sin unidad" : row.UnitCode,
                            IncidentType = string.IsNullOrWhiteSpace(row.IncidentType) ? "-" : row.IncidentType,
                            FailureCategory = string.IsNullOrWhiteSpace(row.FailureCategory) ? "-" : row.FailureCategory,
                            MaintenanceStage = string.IsNullOrWhiteSpace(row.MaintenanceStage) ? "Sin etapa" : row.MaintenanceStage,
                            AssignedTechnician = string.IsNullOrWhiteSpace(row.AssignedTechnician) ? "Sin asignar" : row.AssignedTechnician,
                            Status = row.Status,
                            RequiresCostApproval = row.RequiresCostApproval,
                            CostApproved = row.CostApproved,
                            TotalCostCordoba = dispatchCost.TotalCordoba + row.ComponentCostCordoba + row.LaborCostCordoba + row.ExternalCostCordoba,
                            TotalCostUsd = dispatchCost.TotalUsd + row.ComponentCostUsd + row.LaborCostUsd + row.ExternalCostUsd,
                            RemainingHoursToSla = (row.SLADeadline - nowUtc).TotalHours
                        };
                    })
                    .ToList();

                var boardHistoryByTicket = new Dictionary<int, IReadOnlyList<TicketHistory>>();
                var boardTicketIds = detailSourceRows
                    .Where(x => x.Status != TicketStatus.Closed)
                    .Select(x => x.Id)
                    .Distinct()
                    .ToList();

                if (boardTicketIds.Count > 0)
                {
                    var boardHistoryRows = await _context.TicketHistories
                        .AsNoTracking()
                        .Where(x =>
                            boardTicketIds.Contains(x.TicketId) &&
                            (x.FieldChanged == "Status" || x.FieldChanged == "MaintenanceStage"))
                        .OrderBy(x => x.ChangeDate)
                        .ToListAsync();

                    boardHistoryByTicket = boardHistoryRows
                        .GroupBy(x => x.TicketId)
                        .ToDictionary(
                            g => g.Key,
                            g => (IReadOnlyList<TicketHistory>)g.ToList());
                }

                maintenanceBoardRows = detailSourceRows
                    .Where(row => row.Status != TicketStatus.Closed)
                    .Select(row =>
                    {
                        boardHistoryByTicket.TryGetValue(row.Id, out var historyRows);
                        var startedAtUtc = ResolveMaintenanceBoardStartUtc(
                            row.CreatedDate,
                            historyRows ?? Array.Empty<TicketHistory>());
                        var elapsedHours = Math.Max(0d, (nowUtc - startedAtUtc).TotalHours);
                        var remainingHours = (row.SLADeadline - nowUtc).TotalHours;
                        var (label, tone, rank) = BuildMaintenanceSemaphore(remainingHours, row.Status, row.MaintenanceStage);

                        return new MaintenanceAirportBoardItem
                        {
                            TicketId = row.Id,
                            TicketNumber = string.IsNullOrWhiteSpace(row.TicketNumber) ? $"TK-{row.Id}" : row.TicketNumber!,
                            Technician = string.IsNullOrWhiteSpace(row.AssignedTechnician) ? "Sin asignar" : row.AssignedTechnician,
                            Site = string.IsNullOrWhiteSpace(row.Site) ? "-" : row.Site,
                            UnitCode = string.IsNullOrWhiteSpace(row.UnitCode) ? "Sin unidad" : row.UnitCode,
                            IncidentType = string.IsNullOrWhiteSpace(row.IncidentType) ? "-" : row.IncidentType,
                            MaintenanceStage = string.IsNullOrWhiteSpace(row.MaintenanceStage) ? "Sin etapa" : row.MaintenanceStage,
                            Status = row.Status,
                            StartedAtUtc = startedAtUtc,
                            SlaDeadlineUtc = row.SLADeadline,
                            ElapsedHours = elapsedHours,
                            RemainingHoursToSla = remainingHours,
                            SemaphoreLabel = label,
                            SemaphoreTone = tone,
                            SemaphoreRank = rank
                        };
                    })
                    .OrderBy(x => x.SemaphoreRank)
                    .ThenBy(x => x.RemainingHoursToSla)
                    .ThenBy(x => x.Technician)
                    .Take(120)
                    .ToList();
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)normalizedPageSize));
            var normalizedPage = Math.Clamp(page, 1, totalPages);

            var orderedTicketsQuery = query
                .OrderBy(t => t.Status == TicketStatus.Closed
                    ? 3
                    : t.SLADeadline < nowUtc
                        ? 0
                        : t.SLADeadline < nearThresholdUtc
                            ? 1
                            : 2)
                .ThenBy(t => t.SLADeadline)
                .ThenByDescending(t => t.Priority)
                .ThenByDescending(t => t.CreatedDate);

            // In maintenance default view, prioritize newest creation to avoid hiding freshly created orders.
            if (isMaintenanceView &&
                string.IsNullOrWhiteSpace(normalizedQuick) &&
                string.IsNullOrWhiteSpace(normalizedSearch))
            {
                orderedTicketsQuery = query
                    .OrderByDescending(t => t.CreatedDate)
                    .ThenByDescending(t => t.Priority)
                    .ThenBy(t => t.SLADeadline);
            }

            var tickets = await orderedTicketsQuery
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync();

            var ticketIds = tickets
                .Select(t => t.Id)
                .ToList();

            var latestAuditByTicket = new Dictionary<int, TicketAuditItem>();
            if (ticketIds.Count > 0)
            {
                var relevantFields = new[]
                {
                    "Status",
                    "AssignedTechnician",
                    "Priority",
                    "MaintenanceStage",
                    "CostApproved",
                    FinanceApprovalStepHistoryKey,
                    FinanceWorkflowHistoryKey,
                    FinanceRejectionReasonHistoryKey,
                    FinanceClosureReasonHistoryKey
                };
                var latestAuditRows = await _context.TicketHistories
                    .AsNoTracking()
                    .Where(th =>
                        ticketIds.Contains(th.TicketId) &&
                        ((th.ChangedBy != null && th.ChangedBy.StartsWith(QuickAuditPrefix)) ||
                         th.FieldChanged == FinanceApprovalStepHistoryKey ||
                         th.FieldChanged == FinanceWorkflowHistoryKey ||
                         th.FieldChanged == FinanceRejectionReasonHistoryKey ||
                         th.FieldChanged == FinanceClosureReasonHistoryKey) &&
                        relevantFields.Contains(th.FieldChanged))
                    .OrderByDescending(th => th.ChangeDate)
                    .ToListAsync();

                latestAuditByTicket = latestAuditRows
                    .GroupBy(th => th.TicketId)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var row = g.First();
                            return new TicketAuditItem
                            {
                                TicketId = row.TicketId,
                                FieldChanged = row.FieldChanged,
                                FieldLabel = GetAuditFieldLabel(row.FieldChanged),
                                NewValue = row.NewValue,
                                ChangedBy = StripQuickAuditPrefix(row.ChangedBy),
                                ChangeDate = row.ChangeDate
                            };
                        });
            }

            var model = new TicketsIndexViewModel
            {
                Search = normalizedSearch,
                Status = status,
                Priority = priority,
                QuickFilter = normalizedQuick,
                Department = normalizedDepartment,
                IsMaintenanceView = isMaintenanceView,
                IsFinanceView = isFinanceView,
                IsMineFinanceView = isMineFinanceView,
                SelectedSite = normalizedSite,
                SelectedTenencia = normalizedTenencia,
                SelectedAssignedTechnician = normalizedAssignedTechnician,
                SelectedIncidentType = normalizedIncidentType,
                SelectedMaintenanceStage = normalizedMaintenanceStage,
                SelectedOverdueBand = normalizedOverdueBand,
                MaintenanceViewMode = isMaintenanceView ? normalizedMaintenanceView : "overview",
                FinanceOpenCount = financeOpenCount,
                FinanceInProgressCount = financeInProgressCount,
                FinanceResolvedCount = financeResolvedCount,
                FinanceClosedCount = financeClosedCount,
                FinancePendingApprovalCount = financePendingApprovalCount,
                FinanceRejectedCount = financeRejectedCount,
                FinanceReturnedCount = financeReturnedCount,
                FinanceEscalatedCount = financeEscalatedCount,
                FinanceOverBudgetRequests = financeOverBudgetRequests,
                FinanceBudgetLimitCordoba = financeBudgetLimitCordoba,
                FinanceBudgetCommittedCordoba = financeBudgetCommittedCordoba,
                FinanceBudgetUsagePercent = financeBudgetUsagePercent,
                FinanceApprovedAmountCurrentMonthCordoba = financeApprovedAmountCurrentMonthCordoba,
                FinanceAverageCycleHours = financeAverageCycleHours,
                TotalTickets = totalItems,
                TotalItems = totalItems,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalPages = totalPages,
                ActiveTickets = activeTickets,
                OverdueTickets = overdueTickets,
                NearDueTickets = nearDueTickets,
                UnassignedActiveTickets = unassignedActiveTickets,
                CriticalActiveTickets = criticalActiveTickets,
                SlaComplianceRate = slaComplianceRate,
                CreatedLast7Days = createdLast7Days,
                ClosedLast7Days = closedLast7Days,
                PreventiveOrders = preventiveOrders,
                CorrectiveOrders = correctiveOrders,
                PendingSparePartOrders = pendingSparePartOrders,
                ComponentChangeOrders = componentChangeOrders,
                ClosedTodayOrders = closedTodayOrders,
                MttrHours = mttrHours,
                PendingCostApprovalOrders = pendingCostApprovalOrders,
                TotalMaintenanceCostCordoba = totalMaintenanceCostCordoba,
                TotalMaintenanceCostUsd = totalMaintenanceCostUsd,
                AverageMaintenanceCostCordoba = averageMaintenanceCostCordoba,
                AverageMaintenanceCostUsd = averageMaintenanceCostUsd,
                OrdersWithTrackedCost = ordersWithTrackedCost,
                InventoryLowStockCount = inventoryLowStockCount,
                InventoryOutOfStockCount = inventoryOutOfStockCount,
                InventoryInTransitUnits = inventoryInTransitUnits,
                InventoryHealthTone = inventoryHealthTone,
                InventoryHealthLabel = inventoryHealthLabel,
                OverdueBand0To24Count = overdueBand0To24Count,
                OverdueBand24To72Count = overdueBand24To72Count,
                OverdueBandOver72Count = overdueBandOver72Count,
                TrendLabels = trendLabels,
                TrendCreated = trendCreated,
                TrendClosed = trendClosed,
                TechnicianLoads = technicianLoads,
                TechnicianOptions = technicianOptions,
                SiteOptions = siteOptions,
                TenenciaOptions = tenenciaOptions,
                IncidentTypeOptions = incidentTypeOptions,
                MaintenanceStageOptions = MaintenanceStageOptions,
                StageKpis = stageKpis,
                TechnicianCapacityItems = technicianCapacityItems,
                TechnicianCapacityUtilizationRate = technicianCapacityUtilizationRate,
                MaintenanceSiteCostRanking = maintenanceSiteCostRanking,
                MaintenanceReincidenceRanking = maintenanceReincidenceRanking,
                MaintenanceDetailRows = maintenanceDetailRows,
                MaintenanceBoardRows = maintenanceBoardRows,
                DashboardGeneratedAtUtc = nowUtc,
                LatestAuditByTicket = latestAuditByTicket,
                Tickets = tickets
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MaintenanceBoard(
            string? site,
            string? technician,
            string? stage)
        {
            var nowUtc = DateTime.UtcNow;
            var selectedSite = string.IsNullOrWhiteSpace(site) ? null : site.Trim();
            var selectedTechnician = string.IsNullOrWhiteSpace(technician) ? null : technician.Trim();
            var selectedStage = string.IsNullOrWhiteSpace(stage) ? null : stage.Trim();

            IQueryable<Ticket> query = _context.Tickets
                .AsNoTracking()
                .Where(t =>
                    t.Department == "Mantenimiento" &&
                    t.Status != TicketStatus.Closed);

            if (!string.IsNullOrWhiteSpace(selectedSite))
            {
                query = query.Where(t => t.Site == selectedSite);
            }

            if (!string.IsNullOrWhiteSpace(selectedTechnician))
            {
                query = query.Where(t => t.AssignedTechnician == selectedTechnician);
            }

            if (!string.IsNullOrWhiteSpace(selectedStage))
            {
                query = query.Where(t => t.MaintenanceStage == selectedStage);
            }

            var sourceRows = await query
                .Select(t => new
                {
                    t.Id,
                    t.TicketNumber,
                    t.Site,
                    t.UnitCode,
                    t.IncidentType,
                    t.AssignedTechnician,
                    t.MaintenanceStage,
                    t.Status,
                    t.CreatedDate,
                    t.SLADeadline
                })
                .OrderBy(t => t.SLADeadline)
                .ThenByDescending(t => t.CreatedDate)
                .Take(400)
                .ToListAsync();

            var ticketIds = sourceRows
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            var historyByTicket = new Dictionary<int, IReadOnlyList<TicketHistory>>();
            if (ticketIds.Count > 0)
            {
                var historyRows = await _context.TicketHistories
                    .AsNoTracking()
                    .Where(x =>
                        ticketIds.Contains(x.TicketId) &&
                        (x.FieldChanged == "Status" || x.FieldChanged == "MaintenanceStage"))
                    .OrderBy(x => x.ChangeDate)
                    .ToListAsync();

                historyByTicket = historyRows
                    .GroupBy(x => x.TicketId)
                    .ToDictionary(
                        g => g.Key,
                        g => (IReadOnlyList<TicketHistory>)g.ToList());
            }

            var rows = sourceRows
                .Select(row =>
                {
                    historyByTicket.TryGetValue(row.Id, out var historyRows);
                    var startedAtUtc = ResolveMaintenanceBoardStartUtc(
                        row.CreatedDate,
                        historyRows ?? Array.Empty<TicketHistory>());
                    var elapsedHours = Math.Max(0d, (nowUtc - startedAtUtc).TotalHours);
                    var remainingHours = (row.SLADeadline - nowUtc).TotalHours;
                    var (label, tone, rank) = BuildMaintenanceSemaphore(remainingHours, row.Status, row.MaintenanceStage);

                    return new MaintenanceAirportBoardItem
                    {
                        TicketId = row.Id,
                        TicketNumber = string.IsNullOrWhiteSpace(row.TicketNumber) ? $"TK-{row.Id}" : row.TicketNumber!,
                        Technician = string.IsNullOrWhiteSpace(row.AssignedTechnician) ? "Sin asignar" : row.AssignedTechnician,
                        Site = string.IsNullOrWhiteSpace(row.Site) ? "-" : row.Site,
                        UnitCode = string.IsNullOrWhiteSpace(row.UnitCode) ? "Sin unidad" : row.UnitCode,
                        IncidentType = string.IsNullOrWhiteSpace(row.IncidentType) ? "-" : row.IncidentType,
                        MaintenanceStage = string.IsNullOrWhiteSpace(row.MaintenanceStage) ? "Sin etapa" : row.MaintenanceStage,
                        Status = row.Status,
                        StartedAtUtc = startedAtUtc,
                        SlaDeadlineUtc = row.SLADeadline,
                        ElapsedHours = elapsedHours,
                        RemainingHoursToSla = remainingHours,
                        SemaphoreLabel = label,
                        SemaphoreTone = tone,
                        SemaphoreRank = rank
                    };
                })
                .OrderBy(x => x.SemaphoreRank)
                .ThenBy(x => x.RemainingHoursToSla)
                .ThenBy(x => x.Technician)
                .ToList();

            var siteOptions = MaintenanceSites.ToList();
            if (!string.IsNullOrWhiteSpace(selectedSite) &&
                siteOptions.All(x => !x.Equals(selectedSite, StringComparison.OrdinalIgnoreCase)))
            {
                siteOptions.Insert(0, selectedSite);
            }

            var technicianOptions = await _context.MaintenanceTechnicians
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => x.FullName.Trim())
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(selectedTechnician) &&
                technicianOptions.All(x => !x.Equals(selectedTechnician, StringComparison.OrdinalIgnoreCase)))
            {
                technicianOptions.Insert(0, selectedTechnician);
            }

            var stageOptions = MaintenanceStageOptions.ToList();
            if (!string.IsNullOrWhiteSpace(selectedStage) &&
                stageOptions.All(x => !x.Equals(selectedStage, StringComparison.OrdinalIgnoreCase)))
            {
                stageOptions.Insert(0, selectedStage);
            }

            var model = new MaintenanceBoardViewModel
            {
                GeneratedAtUtc = nowUtc,
                SelectedSite = selectedSite,
                SelectedTechnician = selectedTechnician,
                SelectedStage = selectedStage,
                ActiveOrders = rows.Count,
                OverdueOrders = rows.Count(x => x.RemainingHoursToSla < 0),
                NearDueOrders = rows.Count(x => x.RemainingHoursToSla >= 0 && x.RemainingHoursToSla <= 4),
                UnassignedOrders = rows.Count(x => string.Equals(x.Technician, "Sin asignar", StringComparison.OrdinalIgnoreCase)),
                SiteOptions = siteOptions,
                TechnicianOptions = technicianOptions,
                StageOptions = stageOptions,
                Rows = rows
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetMaintenanceMenuMetrics()
        {
            var nowUtc = DateTime.UtcNow;
            var todayLocal = ToBusinessLocalTime(nowUtc).Date;
            var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal, SlaBusinessTimeZone);
            var tomorrowStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal.AddDays(1), SlaBusinessTimeZone);

            var maintenanceTickets = _context.Tickets
                .AsNoTracking()
                .Where(t => t.Department == "Mantenimiento");

            var overdueOrders = await maintenanceTickets
                .CountAsync(t => t.Status != TicketStatus.Closed && t.SLADeadline < nowUtc);

            var unassignedOrders = await maintenanceTickets
                .CountAsync(t => t.Status != TicketStatus.Closed && (t.AssignedTechnician == null || t.AssignedTechnician == ""));

            var pendingCostApprovals = await maintenanceTickets
                .CountAsync(t => t.Status != TicketStatus.Closed && t.RequiresCostApproval && !t.CostApproved);

            var appointmentsToday = await _context.MaintenanceAppointments
                .AsNoTracking()
                .CountAsync(a =>
                    a.Status != "Cancelada" &&
                    a.ScheduledFor >= todayStartUtc &&
                    a.ScheduledFor < tomorrowStartUtc);

            var inventoryQuery = _context.MaintenanceInventoryParts
                .AsNoTracking()
                .Where(x => x.IsActive);

            var lowStock = await inventoryQuery
                .CountAsync(x => x.QuantityOnHand > 0 && x.QuantityOnHand <= x.MinimumStock);

            var outOfStock = await inventoryQuery
                .CountAsync(x => x.QuantityOnHand <= 0);

            var inTransit = await inventoryQuery
                .Select(x => (int?)x.QuantityInTransit)
                .SumAsync() ?? 0;

            var inventoryHealthTone = outOfStock > 0
                ? "danger"
                : lowStock > 0
                    ? "alert"
                    : "success";

            var inventoryHealthLabel = outOfStock > 0
                ? $"{outOfStock} agotado(s)"
                : lowStock > 0
                    ? $"{lowStock} bajo stock"
                    : "Stock estable";

            return Json(new
            {
                overdueOrders,
                unassignedOrders,
                pendingCostApprovals,
                appointmentsToday,
                lowStock,
                outOfStock,
                inTransit,
                inventoryHealthTone,
                inventoryHealthLabel
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetFinanceMenuMetrics()
        {
            var currentUser = User.Identity?.Name?.Trim();

            var financeQuery = _context.Tickets
                .AsNoTracking()
                .Where(t => t.Department == "Finanzas");

            var mineOpen = 0;
            if (!string.IsNullOrWhiteSpace(currentUser))
            {
                var normalizedCurrentUser = currentUser.Trim();
                mineOpen = await financeQuery.CountAsync(t =>
                    t.Status != TicketStatus.Closed &&
                    t.RequestingUser != null &&
                    t.RequestingUser.ToUpper() == normalizedCurrentUser.ToUpper());
            }

            var pendingApproval = await financeQuery.CountAsync(t =>
                (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress) &&
                !t.CostApproved);

            var inReview = await financeQuery.CountAsync(t => t.Status == TicketStatus.InProgress);
            var closeReady = await financeQuery.CountAsync(t => t.Status == TicketStatus.Resolved && t.CostApproved);
            var returned = await financeQuery.CountAsync(t => t.FinanceReturnedForCorrection && t.Status != TicketStatus.Closed);
            var escalated = await financeQuery.CountAsync(t => t.FinanceEscalatedAtUtc.HasValue && t.Status != TicketStatus.Closed);
            var critical = await financeQuery.CountAsync(t => t.Status != TicketStatus.Closed && t.Priority == PriorityLevel.Critical);
            var closed = await financeQuery.CountAsync(t => t.Status == TicketStatus.Closed);

            return Json(new
            {
                mineOpen,
                pendingApproval,
                inReview,
                closeReady,
                returned,
                escalated,
                critical,
                closed
            });
        }

        private static string? NormalizeQuickFilter(string? quick)
        {
            if (string.IsNullOrWhiteSpace(quick))
            {
                return null;
            }

            var normalized = quick.Trim().ToLowerInvariant();
            return normalized switch
            {
                "today" => "today",
                "overdue" => "overdue",
                "unassigned" => "unassigned",
                "critical" => "critical",
                "myload" => "myload",
                "overdue_unassigned" => "overdue_unassigned",
                "finance_pending_approval" => "finance_pending_approval",
                "finance_close_ready" => "finance_close_ready",
                "finance_rejected" => "finance_rejected",
                "finance_returned" => "finance_returned",
                "finance_escalated" => "finance_escalated",
                _ => null
            };
        }

        private static string? NormalizeOverdueBand(string? overdueBand)
        {
            if (string.IsNullOrWhiteSpace(overdueBand))
            {
                return null;
            }

            var normalized = overdueBand.Trim().ToLowerInvariant();
            return normalized switch
            {
                "0_24" => "0_24",
                "24_72" => "24_72",
                "72_plus" => "72_plus",
                _ => null
            };
        }

        private static string NormalizeMaintenanceViewMode(string? maintenanceView)
        {
            if (string.IsNullOrWhiteSpace(maintenanceView))
            {
                return "overview";
            }

            var normalized = maintenanceView.Trim().ToLowerInvariant();
            return normalized switch
            {
                "detail" => "detail",
                "detalle" => "detail",
                "board" => "board",
                "pizarra" => "board",
                "airport" => "board",
                _ => "overview"
            };
        }

        private static DateTime ResolveMaintenanceBoardStartUtc(
            DateTime createdDateUtc,
            IReadOnlyList<TicketHistory> historyRows)
        {
            if (historyRows.Count == 0)
            {
                return createdDateUtc;
            }

            var startCandidates = historyRows
                .Where(row =>
                    (row.FieldChanged == "Status" && IsInProgressAuditValue(row.NewValue)) ||
                    (row.FieldChanged == "MaintenanceStage" && IsActiveMaintenanceStageAuditValue(row.NewValue)))
                .Select(row => row.ChangeDate)
                .Where(changeDate => changeDate >= createdDateUtc)
                .OrderBy(changeDate => changeDate)
                .ToList();

            return startCandidates.Count > 0 ? startCandidates[0] : createdDateUtc;
        }

        private static bool IsInProgressAuditValue(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "inprogress" ||
                   normalized == "in progress" ||
                   normalized == "en progreso" ||
                   normalized == "en ejecucion";
        }

        private static bool IsActiveMaintenanceStageAuditValue(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "diagnostico" ||
                   normalized == "en reparacion" ||
                   normalized == "prueba tecnica" ||
                   normalized == "lista para entrega";
        }

        private static (string Label, string Tone, int Rank) BuildMaintenanceSemaphore(
            double remainingHours,
            TicketStatus status,
            string? maintenanceStage)
        {
            var normalizedStage = (maintenanceStage ?? string.Empty).Trim();
            if (status == TicketStatus.Closed ||
                normalizedStage.Equals("Cerrada", StringComparison.OrdinalIgnoreCase))
            {
                return ("Completada", "secondary", 3);
            }

            if (remainingHours < 0)
            {
                return ("Vencida", "danger", 0);
            }

            if (remainingHours <= 4)
            {
                return ("Por vencer", "warning", 1);
            }

            if (remainingHours <= 12)
            {
                return ("Seguimiento", "info", 2);
            }

            return ("En tiempo", "success", 2);
        }

        private static IQueryable<Ticket> ApplyQuickFilter(
            IQueryable<Ticket> query,
            string? quick,
            DateTime nowUtc,
            DateTime todayStartUtc,
            DateTime tomorrowStartUtc,
            string? currentUser)
        {
            if (string.IsNullOrWhiteSpace(quick))
            {
                return query;
            }

            return quick switch
            {
                "today" => query.Where(t => t.CreatedDate >= todayStartUtc && t.CreatedDate < tomorrowStartUtc),
                "overdue" => query.Where(t => t.Status != TicketStatus.Closed && t.SLADeadline < nowUtc),
                "unassigned" => query.Where(t => t.Status != TicketStatus.Closed && (t.AssignedTechnician == null || t.AssignedTechnician == "")),
                "critical" => query.Where(t => t.Status != TicketStatus.Closed && t.Priority == PriorityLevel.Critical),
                "overdue_unassigned" => query.Where(t =>
                    t.Status != TicketStatus.Closed &&
                    t.SLADeadline < nowUtc &&
                    (t.AssignedTechnician == null || t.AssignedTechnician == "")),
                "finance_pending_approval" => query.Where(t =>
                    t.Department == "Finanzas" &&
                    (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress) &&
                    !t.CostApproved),
                "finance_close_ready" => query.Where(t =>
                    t.Department == "Finanzas" &&
                    t.Status == TicketStatus.Resolved &&
                    t.CostApproved),
                "finance_rejected" => query.Where(t =>
                    t.Department == "Finanzas" &&
                    t.Status == TicketStatus.Closed &&
                    !t.CostApproved),
                "finance_returned" => query.Where(t =>
                    t.Department == "Finanzas" &&
                    t.FinanceReturnedForCorrection &&
                    t.Status != TicketStatus.Closed),
                "finance_escalated" => query.Where(t =>
                    t.Department == "Finanzas" &&
                    t.FinanceEscalatedAtUtc.HasValue &&
                    t.Status != TicketStatus.Closed),
                "myload" => string.IsNullOrWhiteSpace(currentUser)
                    ? query.Where(_ => false)
                    : query.Where(t =>
                        t.Status != TicketStatus.Closed &&
                        t.AssignedTechnician != null &&
                        t.AssignedTechnician.ToUpper() == currentUser.ToUpper()),
                _ => query
            };
        }

        private static IQueryable<Ticket> ApplyOverdueBandFilter(
            IQueryable<Ticket> query,
            string? overdueBand,
            DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(overdueBand))
            {
                return query;
            }

            var threshold24Utc = nowUtc.AddHours(-24);
            var threshold72Utc = nowUtc.AddHours(-72);

            return overdueBand switch
            {
                "0_24" => query.Where(t =>
                    t.Status != TicketStatus.Closed &&
                    t.SLADeadline < nowUtc &&
                    t.SLADeadline >= threshold24Utc),
                "24_72" => query.Where(t =>
                    t.Status != TicketStatus.Closed &&
                    t.SLADeadline < threshold24Utc &&
                    t.SLADeadline >= threshold72Utc),
                "72_plus" => query.Where(t =>
                    t.Status != TicketStatus.Closed &&
                    t.SLADeadline < threshold72Utc),
                _ => query
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Technician,CoordinadorIT")]
        public async Task<IActionResult> QuickTake(
            int id,
            string? search,
            TicketStatus? status,
            PriorityLevel? priority,
            string? quick,
            string? department,
            string? site,
            string? tenencia,
            string? technician,
            string? incidentType,
            string? maintenanceStage,
            string? overdueBand,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null)
            {
                TempData["TicketsError"] = "No se encontrï¿½ el ticket para tomar.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            if (ticket.Status == TicketStatus.Closed)
            {
                TempData["TicketsError"] = "No se puede tomar un ticket cerrado.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            var currentUser = User.Identity?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(currentUser))
            {
                TempData["TicketsError"] = "No se pudo determinar el usuario actual.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            var originalTicket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (originalTicket == null)
            {
                TempData["TicketsError"] = "No se pudo cargar el estado original del ticket.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            ticket.AssignedTechnician = currentUser;
            if (ticket.Status == TicketStatus.Open)
            {
                ticket.Status = TicketStatus.InProgress;
            }

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, ticket, BuildQuickAuditActor(GetChangedBy()));
                TempData["TicketsMessage"] = $"Ticket {ticket.TicketNumber} tomado correctamente.";
            }
            catch
            {
                TempData["TicketsError"] = "No fue posible tomar el ticket.";
            }

            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Technician,CoordinadorIT")]
        public async Task<IActionResult> QuickAssign(
            int id,
            string? assignedTechnician,
            string? search,
            TicketStatus? status,
            PriorityLevel? priority,
            string? quick,
            string? department,
            string? site,
            string? tenencia,
            string? technician,
            string? incidentType,
            string? maintenanceStage,
            string? overdueBand,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null)
            {
                TempData["TicketsError"] = "No se encontro el ticket para asignar.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            if (ticket.Status == TicketStatus.Closed)
            {
                TempData["TicketsError"] = "No se puede asignar un ticket cerrado.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            var originalTicket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (originalTicket == null)
            {
                TempData["TicketsError"] = "No se pudo cargar el estado original del ticket.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            var normalizedAssignee = string.IsNullOrWhiteSpace(assignedTechnician) ||
                                     assignedTechnician.Equals("Sin asignar", StringComparison.OrdinalIgnoreCase)
                ? null
                : assignedTechnician.Trim();

            if (!string.IsNullOrWhiteSpace(normalizedAssignee))
            {
                var isMaintenanceTicket = string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase);
                if (isMaintenanceTicket)
                {
                    var isSameAssignee = string.Equals(ticket.AssignedTechnician, normalizedAssignee, StringComparison.OrdinalIgnoreCase);
                    var technicianRow = await _context.MaintenanceTechnicians
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.IsActive &&
                            x.FullName == normalizedAssignee &&
                            (x.IsAvailable || isSameAssignee));

                    if (technicianRow == null)
                    {
                        TempData["TicketsError"] = "El tecnico seleccionado no esta disponible en Mantenimiento.";
                        return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
                    }

                    if (!isSameAssignee)
                    {
                        var activeOrders = await _context.Tickets
                            .AsNoTracking()
                            .CountAsync(x =>
                                x.Id != ticket.Id &&
                                x.Department == "Mantenimiento" &&
                                x.Status != TicketStatus.Closed &&
                                x.AssignedTechnician == normalizedAssignee);

                        if (activeOrders >= Math.Max(1, technicianRow.MaxActiveOrders))
                        {
                            TempData["TicketsError"] = $"El tecnico ya alcanzo su capacidad maxima ({technicianRow.MaxActiveOrders}).";
                            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
                        }
                    }
                }
                else
                {
                    var assigneeExists = await _context.Users
                        .AsNoTracking()
                        .AnyAsync(u =>
                            u.IsActive &&
                            u.FullName == normalizedAssignee &&
                            (u.Role == UserRole.Technician ||
                             u.Role == UserRole.CoordinadorIT ||
                             u.Role == UserRole.Administrator));

                    if (!assigneeExists)
                    {
                        TempData["TicketsError"] = "El tecnico seleccionado no es valido.";
                        return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
                    }
                }
            }

            ticket.AssignedTechnician = normalizedAssignee;
            if (!string.IsNullOrWhiteSpace(normalizedAssignee) && ticket.Status == TicketStatus.Open)
            {
                ticket.Status = TicketStatus.InProgress;
            }

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, ticket, BuildQuickAuditActor(GetChangedBy()));
                TempData["TicketsMessage"] = $"Asignacion actualizada para {ticket.TicketNumber}.";
            }
            catch
            {
                TempData["TicketsError"] = "No fue posible actualizar la asignacion.";
            }

            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Technician,CoordinadorIT")]
        public async Task<IActionResult> QuickStatus(
            int id,
            TicketStatus newStatus,
            string? search,
            TicketStatus? status,
            PriorityLevel? priority,
            string? quick,
            string? department,
            string? site,
            string? tenencia,
            string? technician,
            string? incidentType,
            string? maintenanceStage,
            string? overdueBand,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null)
            {
                TempData["TicketsError"] = "No se encontrï¿½ el ticket para cambiar estado.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            if (IsFinanceTicket(ticket))
            {
                TempData["TicketsError"] = "Para Finanzas use el flujo de aprobacion/rechazo/cierre del modulo financiero.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine: false);
            }

            if (ticket.Status == newStatus)
            {
                TempData["TicketsMessage"] = "El ticket ya tenï¿½a ese estado.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            if ((newStatus == TicketStatus.Resolved || newStatus == TicketStatus.Closed) &&
                string.IsNullOrWhiteSpace(ticket.AfterEvidencePath))
            {
                TempData["TicketsError"] = "Para marcar Resuelto/Cerrado se requiere evidencia final.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            var isMaintenanceTicket = string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase);
            if (isMaintenanceTicket && newStatus == TicketStatus.Closed)
            {
                if (ticket.RequiresCostApproval && !ticket.CostApproved)
                {
                    TempData["TicketsError"] = "No se puede cerrar la orden mientras la aprobacion de costos este pendiente.";
                    return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
                }

                var stage = ticket.MaintenanceStage?.Trim() ?? string.Empty;
                if (!stage.Equals("Lista para entrega", StringComparison.OrdinalIgnoreCase) &&
                    !stage.Equals("Cerrada", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["TicketsError"] = "Para cerrar rapido la orden, la etapa debe estar en 'Lista para entrega' o 'Cerrada'.";
                    return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
                }
            }

            var originalTicket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (originalTicket == null)
            {
                TempData["TicketsError"] = "No se pudo cargar el estado original del ticket.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            ticket.Status = newStatus;
            if (isMaintenanceTicket && newStatus == TicketStatus.Closed)
            {
                ticket.MaintenanceStage = "Cerrada";
            }

            if (newStatus == TicketStatus.Closed && originalTicket.Status != TicketStatus.Closed)
            {
                ticket.ClosedDate = DateTime.UtcNow;
            }

            if (newStatus != TicketStatus.Closed && originalTicket.Status == TicketStatus.Closed)
            {
                ticket.ClosedDate = null;
            }

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, ticket, BuildQuickAuditActor(GetChangedBy()));
                TempData["TicketsMessage"] = $"Estado actualizado para {ticket.TicketNumber}.";
            }
            catch
            {
                TempData["TicketsError"] = "No fue posible actualizar el estado.";
            }

            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral")]
        public async Task<IActionResult> ApproveMaintenanceCost(
            int id,
            string? approvalNotes,
            string? search,
            TicketStatus? status,
            PriorityLevel? priority,
            string? quick,
            string? department,
            string? site,
            string? tenencia,
            string? technician,
            string? incidentType,
            string? maintenanceStage,
            string? overdueBand,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null)
            {
                TempData["TicketsError"] = "No se encontro la orden para aprobar.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            if (!string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase))
            {
                TempData["TicketsError"] = "La aprobacion de costos solo aplica a ordenes de mantenimiento.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            var originalTicket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (originalTicket == null)
            {
                TempData["TicketsError"] = "No se pudo cargar la orden original para aprobar.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
            }

            ticket.CostApproved = true;
            ticket.CostApprovedBy = GetChangedBy();
            ticket.CostApprovedAtUtc = DateTime.UtcNow;
            ticket.CostApprovalNotes = string.IsNullOrWhiteSpace(approvalNotes) ? null : approvalNotes.Trim();

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, ticket, BuildQuickAuditActor(GetChangedBy()));
                TempData["TicketsMessage"] = $"Costos aprobados para {ticket.TicketNumber}.";
            }
            catch
            {
                TempData["TicketsError"] = "No fue posible aprobar los costos de la orden.";
            }

            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral")]
        public async Task<IActionResult> ApproveFinanceRequest(
            int id,
            string? approvalNotes,
            bool mine = false,
            string? search = null,
            TicketStatus? status = null,
            PriorityLevel? priority = null,
            string? quick = null,
            string? department = null,
            string? site = null,
            string? tenencia = null,
            string? technician = null,
            string? incidentType = null,
            string? maintenanceStage = null,
            string? overdueBand = null,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null)
            {
                TempData["TicketsError"] = "No se encontro la solicitud financiera.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            if (!IsFinanceTicket(ticket))
            {
                TempData["TicketsError"] = "Esta accion solo aplica a solicitudes de Finanzas.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var changedBy = GetChangedBy();
            if (IsRequesterSameAsApprover(ticket, changedBy))
            {
                TempData["TicketsError"] = "Segregacion de funciones: el solicitante no puede aprobar su propia solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var metadata = ParseFinanceTicketMeta(ticket);
            var approvalPolicy = await GetFinanceApprovalPolicyAsync(
                metadata.RequestType,
                metadata.CostCenter,
                metadata.Amount,
                metadata.Currency);
            var requiredApprovals = metadata.RequiredApprovals > 0
                ? metadata.RequiredApprovals
                : approvalPolicy.RequiredApprovals;

            var existingApprovers = await GetFinanceApproversAsync(ticket.Id);
            if (existingApprovers.Contains(changedBy, StringComparer.OrdinalIgnoreCase))
            {
                TempData["TicketsError"] = "Ya registraste tu firma de aprobacion para esta solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var originalTicket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (originalTicket == null)
            {
                TempData["TicketsError"] = "No se encontro el estado original de la solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var approvalsAfterThisAction = existingApprovers.Count + 1;
            ticket.RequiresCostApproval = requiredApprovals > 0;
            if (approvalsAfterThisAction >= requiredApprovals)
            {
                ticket.CostApproved = true;
                ticket.CostApprovedBy = changedBy;
                ticket.CostApprovedAtUtc = DateTime.UtcNow;
                ticket.Status = TicketStatus.Resolved;
            }
            else
            {
                ticket.Status = TicketStatus.InProgress;
            }

            ticket.CostApprovalNotes = AppendFinanceNote(
                ticket.CostApprovalNotes,
                string.IsNullOrWhiteSpace(approvalNotes)
                    ? $"Aprobacion parcial registrada por {changedBy} ({approvalsAfterThisAction}/{requiredApprovals})."
                    : approvalNotes.Trim());

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, ticket, BuildQuickAuditActor(changedBy));

                _context.TicketHistories.Add(CreateHistory(
                    ticket.Id,
                    FinanceApprovalStepHistoryKey,
                    $"{existingApprovers.Count}/{requiredApprovals}",
                    $"{approvalsAfterThisAction}/{requiredApprovals}",
                    changedBy));

                if (approvalsAfterThisAction >= requiredApprovals)
                {
                    _context.TicketHistories.Add(CreateHistory(
                        ticket.Id,
                        FinanceWorkflowHistoryKey,
                        "En evaluacion",
                        "Lista para cierre",
                        changedBy));
                }

                await _context.SaveChangesAsync();
                TempData["TicketsMessage"] = approvalsAfterThisAction >= requiredApprovals
                    ? $"Solicitud {ticket.TicketNumber} aprobada y lista para cierre."
                    : $"Firma registrada ({approvalsAfterThisAction}/{requiredApprovals}) para {ticket.TicketNumber}.";
            }
            catch
            {
                TempData["TicketsError"] = "No fue posible registrar la aprobacion.";
            }

            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral")]
        public async Task<IActionResult> RejectFinanceRequest(
            int id,
            string rejectionReason,
            bool mine = false,
            string? search = null,
            TicketStatus? status = null,
            PriorityLevel? priority = null,
            string? quick = null,
            string? department = null,
            string? site = null,
            string? tenencia = null,
            string? technician = null,
            string? incidentType = null,
            string? maintenanceStage = null,
            string? overdueBand = null,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null)
            {
                TempData["TicketsError"] = "No se encontro la solicitud financiera.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            if (!IsFinanceTicket(ticket))
            {
                TempData["TicketsError"] = "Esta accion solo aplica a solicitudes de Finanzas.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["TicketsError"] = "Debes indicar un motivo para rechazar la solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var changedBy = GetChangedBy();
            if (IsRequesterSameAsApprover(ticket, changedBy))
            {
                TempData["TicketsError"] = "Segregacion de funciones: el solicitante no puede rechazar su propia solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var originalTicket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (originalTicket == null)
            {
                TempData["TicketsError"] = "No se encontro el estado original de la solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            ticket.Status = TicketStatus.Closed;
            ticket.CostApproved = false;
            ticket.CostApprovedBy = changedBy;
            ticket.CostApprovedAtUtc = DateTime.UtcNow;
            ticket.ClosedDate = DateTime.UtcNow;
            ticket.FinanceReturnedForCorrection = false;
            ticket.CostApprovalNotes = AppendFinanceNote(ticket.CostApprovalNotes, $"Rechazada: {rejectionReason.Trim()}");

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, ticket, BuildQuickAuditActor(changedBy));
                _context.TicketHistories.Add(CreateHistory(ticket.Id, FinanceWorkflowHistoryKey, "En evaluacion", "Rechazada", changedBy));
                _context.TicketHistories.Add(CreateHistory(ticket.Id, FinanceRejectionReasonHistoryKey, null, rejectionReason.Trim(), changedBy));
                await _context.SaveChangesAsync();
                await NotifyTicketClosedAsync(ticket);
                TempData["TicketsMessage"] = $"Solicitud {ticket.TicketNumber} rechazada.";
            }
            catch
            {
                TempData["TicketsError"] = "No fue posible rechazar la solicitud.";
            }

            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral")]
        public async Task<IActionResult> CloseFinanceRequest(
            int id,
            string closureNotes,
            bool mine = false,
            string? search = null,
            TicketStatus? status = null,
            PriorityLevel? priority = null,
            string? quick = null,
            string? department = null,
            string? site = null,
            string? tenencia = null,
            string? technician = null,
            string? incidentType = null,
            string? maintenanceStage = null,
            string? overdueBand = null,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null)
            {
                TempData["TicketsError"] = "No se encontro la solicitud financiera.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            if (!IsFinanceTicket(ticket))
            {
                TempData["TicketsError"] = "Esta accion solo aplica a solicitudes de Finanzas.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            if (string.IsNullOrWhiteSpace(closureNotes))
            {
                TempData["TicketsError"] = "Debes indicar las notas de cierre.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var changedBy = GetChangedBy();
            if (IsRequesterSameAsApprover(ticket, changedBy))
            {
                TempData["TicketsError"] = "Segregacion de funciones: el solicitante no puede cerrar su propia solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var metadata = ParseFinanceTicketMeta(ticket);
            var approvalPolicy = await GetFinanceApprovalPolicyAsync(
                metadata.RequestType,
                metadata.CostCenter,
                metadata.Amount,
                metadata.Currency);
            var requiredApprovals = metadata.RequiredApprovals > 0
                ? metadata.RequiredApprovals
                : approvalPolicy.RequiredApprovals;
            var approvalCount = await GetFinanceApprovalCountAsync(ticket.Id);

            var hasSupportDocument = !string.IsNullOrWhiteSpace(ticket.AttachmentPath);
            var hasReference = !string.IsNullOrWhiteSpace(metadata.ReferenceNumber);
            var hasJustification = !string.IsNullOrWhiteSpace(metadata.BusinessJustification);
            var approvalsCompleted = ticket.CostApproved && approvalCount >= requiredApprovals;
            var hasAccountingTrace = !string.IsNullOrWhiteSpace(ticket.FinanceAccountingEntryNumber) &&
                                     !string.IsNullOrWhiteSpace(ticket.FinanceInvoiceNumber) &&
                                     !string.IsNullOrWhiteSpace(ticket.FinanceFinalPaymentMethod) &&
                                     ticket.FinancePaymentDateUtc.HasValue &&
                                     string.Equals(ticket.FinanceReconciliationStatus, "Conciliado", StringComparison.OrdinalIgnoreCase);
            var typeChecklistComplete = IsFinanceTypeChecklistComplete(metadata, ticket);

            if (!hasSupportDocument || !hasReference || !hasJustification || !approvalsCompleted || !hasAccountingTrace || !typeChecklistComplete)
            {
                var pendingChecks = new List<string>();
                if (!hasSupportDocument) pendingChecks.Add("Documento de soporte");
                if (!hasReference) pendingChecks.Add("Referencia de factura/OC");
                if (!hasJustification) pendingChecks.Add("Justificacion de negocio");
                if (!approvalsCompleted) pendingChecks.Add("Aprobaciones completas");
                if (!hasAccountingTrace) pendingChecks.Add("Trazabilidad contable completa");
                if (!typeChecklistComplete) pendingChecks.Add("Checklist por tipo de solicitud");

                TempData["TicketsError"] = $"Checklist de cierre incompleto: {string.Join(", ", pendingChecks)}.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var originalTicket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (originalTicket == null)
            {
                TempData["TicketsError"] = "No se encontro el estado original de la solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            ticket.Status = TicketStatus.Closed;
            ticket.ClosedDate = DateTime.UtcNow;
            ticket.CostApprovalNotes = AppendFinanceNote(ticket.CostApprovalNotes, $"Cierre: {closureNotes.Trim()}");

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, ticket, BuildQuickAuditActor(changedBy));
                _context.TicketHistories.Add(CreateHistory(ticket.Id, FinanceWorkflowHistoryKey, "Lista para cierre", "Cerrada", changedBy));
                _context.TicketHistories.Add(CreateHistory(ticket.Id, FinanceClosureReasonHistoryKey, null, closureNotes.Trim(), changedBy));
                await _context.SaveChangesAsync();
                await NotifyTicketClosedAsync(ticket);
                TempData["TicketsMessage"] = $"Solicitud {ticket.TicketNumber} cerrada correctamente.";
            }
            catch
            {
                TempData["TicketsError"] = "No fue posible cerrar la solicitud.";
            }

            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
        }

        [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral")]
        public async Task<IActionResult> FinanceAccounting(
            int id,
            bool mine = false,
            string? search = null,
            TicketStatus? status = null,
            PriorityLevel? priority = null,
            string? quick = null,
            string? department = null,
            string? site = null,
            string? tenencia = null,
            string? technician = null,
            string? incidentType = null,
            string? maintenanceStage = null,
            string? overdueBand = null,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null || !IsFinanceTicket(ticket))
            {
                TempData["TicketsError"] = "No se encontro la solicitud financiera.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var meta = ParseFinanceTicketMeta(ticket);
            var vm = new FinanceAccountingViewModel
            {
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber ?? $"TK-{ticket.Id:D4}",
                RequestType = string.IsNullOrWhiteSpace(meta.RequestType) ? ticket.IncidentType : meta.RequestType,
                CostCenter = meta.CostCenter,
                AccountingEntryNumber = ticket.FinanceAccountingEntryNumber,
                PurchaseOrderNumber = ticket.FinancePurchaseOrderNumber,
                InvoiceNumber = ticket.FinanceInvoiceNumber,
                PaymentDate = ticket.FinancePaymentDateUtc?.Date,
                FinalPaymentMethod = ticket.FinanceFinalPaymentMethod,
                ReconciliationStatus = ticket.FinanceReconciliationStatus,
                ErpExportReference = ticket.FinanceErpExportReference
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral")]
        public async Task<IActionResult> FinanceAccounting(
            FinanceAccountingViewModel model,
            bool mine = false,
            string? search = null,
            TicketStatus? status = null,
            PriorityLevel? priority = null,
            string? quick = null,
            string? department = null,
            string? site = null,
            string? tenencia = null,
            string? technician = null,
            string? incidentType = null,
            string? maintenanceStage = null,
            string? overdueBand = null,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == model.TicketId);
            if (ticket == null || !IsFinanceTicket(ticket))
            {
                TempData["TicketsError"] = "No se encontro la solicitud financiera.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var originalTicket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == model.TicketId);
            if (originalTicket == null)
            {
                TempData["TicketsError"] = "No se encontro el estado original de la solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            ticket.FinanceAccountingEntryNumber = string.IsNullOrWhiteSpace(model.AccountingEntryNumber) ? null : model.AccountingEntryNumber.Trim();
            ticket.FinancePurchaseOrderNumber = string.IsNullOrWhiteSpace(model.PurchaseOrderNumber) ? null : model.PurchaseOrderNumber.Trim();
            ticket.FinanceInvoiceNumber = string.IsNullOrWhiteSpace(model.InvoiceNumber) ? null : model.InvoiceNumber.Trim();
            ticket.FinancePaymentDateUtc = model.PaymentDate?.Date;
            ticket.FinanceFinalPaymentMethod = string.IsNullOrWhiteSpace(model.FinalPaymentMethod) ? null : model.FinalPaymentMethod.Trim();
            ticket.FinanceReconciliationStatus = string.IsNullOrWhiteSpace(model.ReconciliationStatus) ? null : model.ReconciliationStatus.Trim();
            ticket.FinanceErpExportReference = string.IsNullOrWhiteSpace(model.ErpExportReference) ? null : model.ErpExportReference.Trim();

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, ticket, BuildQuickAuditActor(GetChangedBy()));
                _context.TicketHistories.Add(CreateHistory(
                    ticket.Id,
                    FinanceAccountingTraceHistoryKey,
                    null,
                    $"Asiento:{ticket.FinanceAccountingEntryNumber ?? "N/A"} Factura:{ticket.FinanceInvoiceNumber ?? "N/A"} Conciliacion:{ticket.FinanceReconciliationStatus ?? "N/A"}",
                    GetChangedBy()));
                await _context.SaveChangesAsync();
                TempData["TicketsMessage"] = $"Trazabilidad contable actualizada para {ticket.TicketNumber}.";
            }
            catch
            {
                TempData["TicketsError"] = "No fue posible actualizar la trazabilidad contable.";
            }

            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT,GerenciaGeneral")]
        public async Task<IActionResult> ReturnFinanceRequest(
            int id,
            string returnReason,
            bool mine = false,
            string? search = null,
            TicketStatus? status = null,
            PriorityLevel? priority = null,
            string? quick = null,
            string? department = null,
            string? site = null,
            string? tenencia = null,
            string? technician = null,
            string? incidentType = null,
            string? maintenanceStage = null,
            string? overdueBand = null,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null || !IsFinanceTicket(ticket))
            {
                TempData["TicketsError"] = "No se encontro la solicitud financiera.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            if (string.IsNullOrWhiteSpace(returnReason))
            {
                TempData["TicketsError"] = "Debes indicar motivo de devolución.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var originalTicket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (originalTicket == null)
            {
                TempData["TicketsError"] = "No se encontro el estado original de la solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var changedBy = GetChangedBy();
            ticket.Status = TicketStatus.Open;
            ticket.CostApproved = false;
            ticket.CostApprovedBy = null;
            ticket.CostApprovedAtUtc = null;
            ticket.FinanceReturnedForCorrection = true;
            ticket.FinanceReturnedAtUtc = DateTime.UtcNow;
            ticket.FinanceReturnedBy = changedBy;
            ticket.FinanceReturnReason = returnReason.Trim();
            ticket.CostApprovalNotes = AppendFinanceNote(ticket.CostApprovalNotes, $"Devuelta para correccion: {ticket.FinanceReturnReason}");

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, ticket, BuildQuickAuditActor(changedBy));
                _context.TicketHistories.Add(CreateHistory(ticket.Id, FinanceWorkflowHistoryKey, "En evaluacion", "Devuelta para correccion", changedBy));
                _context.TicketHistories.Add(CreateHistory(ticket.Id, FinanceReturnForCorrectionHistoryKey, null, ticket.FinanceReturnReason, changedBy));
                await _context.SaveChangesAsync();
                TempData["TicketsMessage"] = $"Solicitud {ticket.TicketNumber} devuelta al solicitante para corrección.";
            }
            catch
            {
                TempData["TicketsError"] = "No fue posible devolver la solicitud para corrección.";
            }

            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitFinanceCorrection(
            int id,
            string correctionNotes,
            bool mine = false,
            string? search = null,
            TicketStatus? status = null,
            PriorityLevel? priority = null,
            string? quick = null,
            string? department = null,
            string? site = null,
            string? tenencia = null,
            string? technician = null,
            string? incidentType = null,
            string? maintenanceStage = null,
            string? overdueBand = null,
            int page = 1,
            int pageSize = 25)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null || !IsFinanceTicket(ticket))
            {
                TempData["TicketsError"] = "No se encontro la solicitud financiera.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            if (!ticket.FinanceReturnedForCorrection)
            {
                TempData["TicketsError"] = "La solicitud no está en estado devuelto para corrección.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            if (string.IsNullOrWhiteSpace(correctionNotes))
            {
                TempData["TicketsError"] = "Debes ingresar nota de corrección.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var changedBy = GetChangedBy();
            var isSameRequester = !string.IsNullOrWhiteSpace(ticket.RequestingUser) &&
                                  ticket.RequestingUser.Trim().Equals(changedBy.Trim(), StringComparison.OrdinalIgnoreCase);
            var isFinanceAdmin = User.IsInRole(nameof(UserRole.Administrator)) ||
                                 User.IsInRole(nameof(UserRole.CoordinadorIT)) ||
                                 User.IsInRole(nameof(UserRole.GerenciaGeneral));
            if (!isSameRequester && !isFinanceAdmin)
            {
                TempData["TicketsError"] = "Solo el solicitante o un aprobador puede reenviar la corrección.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            var originalTicket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (originalTicket == null)
            {
                TempData["TicketsError"] = "No se encontro el estado original de la solicitud.";
                return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
            }

            ticket.FinanceReturnedForCorrection = false;
            ticket.FinanceReturnedAtUtc = null;
            ticket.FinanceReturnedBy = null;
            ticket.Status = TicketStatus.InProgress;
            ticket.CostApprovalNotes = AppendFinanceNote(ticket.CostApprovalNotes, $"Correccion enviada: {correctionNotes.Trim()}");

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, ticket, BuildQuickAuditActor(changedBy));
                _context.TicketHistories.Add(CreateHistory(ticket.Id, FinanceWorkflowHistoryKey, "Devuelta para correccion", "En evaluacion", changedBy));
                _context.TicketHistories.Add(CreateHistory(ticket.Id, FinanceCorrectionSubmittedHistoryKey, null, correctionNotes.Trim(), changedBy));
                await _context.SaveChangesAsync();
                TempData["TicketsMessage"] = $"Corrección enviada para {ticket.TicketNumber}.";
            }
            catch
            {
                TempData["TicketsError"] = "No fue posible reenviar la corrección.";
            }

            return RedirectToIndexWithContext(search, status, priority, quick, department, site, tenencia, technician, incidentType, overdueBand, page, pageSize, maintenanceStage, mine);
        }

        private IActionResult RedirectToIndexWithContext(
            string? search,
            TicketStatus? status,
            PriorityLevel? priority,
            string? quick,
            string? department,
            string? site,
            string? tenencia,
            string? technician,
            string? incidentType,
            string? overdueBand,
            int page,
            int pageSize,
            string? maintenanceStage = null,
            bool mine = false)
        {
            return RedirectToAction(nameof(Index), new
            {
                search,
                status,
                priority,
                quick,
                mine,
                department,
                site,
                tenencia,
                technician,
                incidentType,
                maintenanceStage,
                overdueBand,
                page,
                pageSize
            });
        }

        private static string GetAuditFieldLabel(string fieldChanged)
        {
            return fieldChanged switch
            {
                "Creacion" => "Creacion",
                "Status" => "Estado",
                "AssignedTechnician" => "Tecnico",
                "TechnicianCategory" => "Categoria tecnica",
                "Priority" => "Prioridad",
                "MaintenanceStage" => "Etapa taller",
                "RequestingUser" => "Solicitante",
                "Site" => "Sitio",
                "Tenencia" => "Tenencia",
                "Department" => "Departamento",
                "IncidentType" => "Tipo de mantenimiento",
                "UnitCode" => "Unidad / Equipo / Area",
                "FailureCategory" => "Categoria de falla",
                "DamagedElement" => "Elemento danado",
                "FailureOdometerKm" => "KM ingreso falla",
                "ExitOdometerKm" => "KM salida taller",
                "Description" => "Descripcion",
                "InitialConditionNotes" => "Condicion inicial",
                "RepairActionsPerformed" => "Reparaciones realizadas",
                "RootCause" => "Causa raiz",
                "TechnicalTestsPerformed" => "Pruebas tecnicas",
                "SparePartRequired" => "Requirio repuesto",
                "SparePartPurchased" => "Repuesto comprado",
                "SparePartDetails" => "Detalle repuestos",
                "ComponentChanged" => "Cambio de componente",
                "ChangedComponentName" => "Componente cambiado",
                "ChangedComponentCostCordoba" => "Costo componente C$",
                "ChangedComponentCostUsd" => "Costo componente $",
                "LaborCostCordoba" => "Mano de obra C$",
                "LaborCostUsd" => "Mano de obra $",
                "ExternalCostCordoba" => "Costo externo C$",
                "ExternalCostUsd" => "Costo externo $",
                "RequiresCostApproval" => "Requiere aprobacion de costo",
                "CostApproved" => "Aprobacion costo",
                "CostApprovedBy" => "Aprobado por",
                "CostApprovedAtUtc" => "Fecha aprobacion",
                "CostApprovalNotes" => "Notas aprobacion",
                "UserConformityConfirmed" => "Conformidad usuario",
                "PreventiveRecommendations" => "Recomendaciones preventivas",
                "Observations" => "Observaciones",
                "BeforeEvidencePath" => "Evidencia inicial",
                "AfterEvidencePath" => "Evidencia final",
                "TechnicalSheetPath" => "Ficha tecnica",
                "ExitOrderPath" => "Orden de salida",
                "AttachmentPath" => "Adjunto principal",
                "SLADeadline" => "Fecha limite SLA",
                "ClosedDate" => "Fecha cierre",
                "FinanceAccountingEntryNumber" => "Asiento contable",
                "FinancePurchaseOrderNumber" => "Orden de compra",
                "FinanceInvoiceNumber" => "Factura",
                "FinancePaymentDateUtc" => "Fecha de pago",
                "FinanceFinalPaymentMethod" => "Metodo de pago",
                "FinanceReconciliationStatus" => "Estado de conciliacion",
                "FinanceErpExportReference" => "Referencia ERP",
                "FinanceReturnedForCorrection" => "Devuelta para correccion",
                "FinanceEscalatedAtUtc" => "Escalacion",
                FinanceApprovalStepHistoryKey => "Firma de aprobacion",
                FinanceWorkflowHistoryKey => "Flujo financiero",
                FinanceRejectionReasonHistoryKey => "Motivo rechazo",
                FinanceClosureReasonHistoryKey => "Motivo cierre",
                FinanceReturnForCorrectionHistoryKey => "Motivo devolucion",
                FinanceCorrectionSubmittedHistoryKey => "Nota de correccion",
                FinanceAccountingTraceHistoryKey => "Trazabilidad contable",
                FinanceAlertEscalationHistoryKey => "Escalacion financiera",
                _ => fieldChanged
            };
        }

        private static string BuildQuickAuditActor(string actor)
        {
            if (string.IsNullOrWhiteSpace(actor))
            {
                return QuickAuditPrefix + "Sistema";
            }

            return actor.StartsWith(QuickAuditPrefix, StringComparison.Ordinal)
                ? actor
                : QuickAuditPrefix + actor;
        }

        private static string StripQuickAuditPrefix(string? actor)
        {
            if (string.IsNullOrWhiteSpace(actor))
            {
                return "Sistema";
            }

            return actor.StartsWith(QuickAuditPrefix, StringComparison.Ordinal)
                ? actor.Substring(QuickAuditPrefix.Length)
                : actor;
        }

        public async Task<IActionResult> Details(int? id, string? department = null)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets
                .AsNoTracking()
                .Include(t => t.HistoryEntries)
                .Include(t => t.SparePartDispatches)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            ViewBag.ReturnDepartment = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            return View(ticket);
        }

        public async Task<IActionResult> DownloadPdf(int id, string? department = null)
        {
            var ticket = await _context.Tickets
                .AsNoTracking()
                .Include(t => t.HistoryEntries)
                .Include(t => t.SparePartDispatches)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            var safeTicket = string.IsNullOrWhiteSpace(ticket.TicketNumber)
                ? $"Ticket_{ticket.Id}"
                : ticket.TicketNumber;
            var fileSuffix = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            if (IsMaintenancePdfTicket(ticket, department))
            {
                var maintenancePdf = MaintenanceOrderPdfReportService.GeneratePdf(ticket, _environment.WebRootPath);
                return File(maintenancePdf, "application/pdf", $"OrdenMantenimiento_{safeTicket}_{fileSuffix}.pdf");
            }

            var ticketPdf = TicketPdfReportService.GenerateTicketReportPdf(ticket, _environment.WebRootPath);
            return File(ticketPdf, "application/pdf", $"Reporte_{safeTicket}_{fileSuffix}.pdf");
        }

        private static bool IsMaintenancePdfTicket(Ticket ticket, string? routeDepartment = null)
        {
            var normalizedRouteDepartment = routeDepartment?.Trim();
            if (string.Equals(normalizedRouteDepartment, "Mantenimiento", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalizedDepartment = ticket.Department?.Trim();
            if (string.Equals(normalizedDepartment, "Mantenimiento", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var incidentType = ticket.IncidentType?.Trim();
            if (!string.IsNullOrWhiteSpace(incidentType) &&
                incidentType.Contains("Mantenimiento", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(ticket.Tenencia) ||
                   !string.IsNullOrWhiteSpace(ticket.UnitCode) ||
                   !string.IsNullOrWhiteSpace(ticket.FailureCategory) ||
                   !string.IsNullOrWhiteSpace(ticket.DamagedElement);
        }

        public async Task<IActionResult> CreateArticleOutput(string? warehouse = null)
        {
            var model = new ArticleOutputCreateViewModel
            {
                DispatchDateTime = DateTime.Now,
                DispatchWarehouse = string.IsNullOrWhiteSpace(warehouse) ? string.Empty : warehouse.Trim(),
                DispatchStatus = "PENDIENTE",
                UserEmail = GetChangedBy()
            };

            await PopulateArticleOutputFormDataAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateArticleOutput(
            ArticleOutputCreateViewModel model,
            IFormFile? outputPhotoFile)
        {
            model.RequisitionNumber = model.RequisitionNumber?.Trim() ?? string.Empty;
            model.TargetEquipment = model.TargetEquipment?.Trim() ?? string.Empty;
            model.DispatchWarehouse = model.DispatchWarehouse?.Trim() ?? string.Empty;
            model.ReceivedBy = model.ReceivedBy?.Trim() ?? string.Empty;
            model.DispatchStatus = NormalizeArticleOutputStatus(model.DispatchStatus);
            model.SignatureName = string.IsNullOrWhiteSpace(model.SignatureName) ? null : model.SignatureName.Trim();
            model.UserEmail = string.IsNullOrWhiteSpace(model.UserEmail) ? GetChangedBy() : model.UserEmail.Trim();
            model.AdditionalNotes = string.IsNullOrWhiteSpace(model.AdditionalNotes) ? null : model.AdditionalNotes.Trim();

            var sparePartInputs = ParseMaintenanceSparePartInputs(model.SparePartsJson);
            model.SparePartsJson = SerializeMaintenanceSparePartInputs(sparePartInputs);

            if (model.DispatchDateTime == default)
            {
                model.DispatchDateTime = DateTime.Now;
            }

            if (!ArticleOutputStatuses.Contains(model.DispatchStatus, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.DispatchStatus), "Selecciona un estado valido para la salida.");
            }
            else
            {
                model.DispatchStatus = ArticleOutputStatuses
                    .First(x => x.Equals(model.DispatchStatus, StringComparison.OrdinalIgnoreCase));
            }

            if (sparePartInputs.Count == 0)
            {
                ModelState.AddModelError(SparePartsModelStateKey, "Debes agregar al menos un articulo despachado.");
            }

            var availableWarehouses = PurchaseRequestSites
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            if (string.IsNullOrWhiteSpace(model.DispatchWarehouse) ||
                availableWarehouses.All(x => !x.Equals(model.DispatchWarehouse, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(model.DispatchWarehouse), "Selecciona un almacen de despacho valido.");
            }

            var selectedPartsById = new Dictionary<int, MaintenanceInventoryPart>();
            if (sparePartInputs.Count > 0)
            {
                var requestedIds = sparePartInputs
                    .Select(x => x.PartId)
                    .Distinct()
                    .ToList();

                selectedPartsById = await _context.MaintenanceInventoryParts
                    .AsNoTracking()
                    .Where(x => x.IsActive && requestedIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                foreach (var item in sparePartInputs)
                {
                    if (!selectedPartsById.TryGetValue(item.PartId, out var part))
                    {
                        ModelState.AddModelError(SparePartsModelStateKey, "Hay articulos seleccionados que ya no estan disponibles.");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(model.DispatchWarehouse) &&
                        !part.Site.Equals(model.DispatchWarehouse, StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(
                            SparePartsModelStateKey,
                            $"El articulo {part.PartCode} - {part.PartName} no pertenece al almacen seleccionado.");
                    }

                    if (part.QuantityOnHand < item.Quantity)
                    {
                        ModelState.AddModelError(
                            SparePartsModelStateKey,
                            $"Stock insuficiente para {part.PartCode} - {part.PartName}. Disponible: {part.QuantityOnHand}.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateArticleOutputFormDataAsync(model, model.SparePartsJson);
                return View(model);
            }

            var dispatchUtc = DateTime.SpecifyKind(model.DispatchDateTime, DateTimeKind.Local).ToUniversalTime();
            var status = MapArticleOutputTicketStatus(model.DispatchStatus);

            var ticket = new Ticket
            {
                RequestingUser = model.ReceivedBy,
                Site = model.DispatchWarehouse,
                UnitCode = model.TargetEquipment,
                Department = "Inventario",
                IncidentType = "Salida de articulos",
                Priority = PriorityLevel.Medium,
                Status = status,
                Description = BuildArticleOutputDescription(model, sparePartInputs, selectedPartsById),
                Observations = BuildArticleOutputObservations(model),
                SparePartRequired = sparePartInputs.Count > 0,
                SparePartPurchased = false,
                SparePartDetails = string.Join(", ",
                    sparePartInputs
                        .Where(x => selectedPartsById.ContainsKey(x.PartId))
                        .Select(x =>
                        {
                            var part = selectedPartsById[x.PartId];
                            return $"{part.PartCode} {part.PartName} x{x.Quantity}";
                        })),
                RequiresCostApproval = false,
                CostApproved = true,
                CostApprovedBy = GetChangedBy(),
                CostApprovedAtUtc = dispatchUtc,
                CreatedDate = dispatchUtc,
                SLADeadline = CalculateSLA(PriorityLevel.Medium),
                ClosedDate = status == TicketStatus.Closed ? dispatchUtc : null
            };

            ticket.AttachmentPath = await SaveUploadedFileAsync(
                outputPhotoFile,
                ticket.AttachmentPath,
                new[] { ".jpg", ".jpeg", ".png", ".webp" });

            const int maxAttempts = 3;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ticket.TicketNumber = await GenerateTicketNumberAsync();
                var stockValidationFailed = false;
                var stockValidationMessage = string.Empty;
                var executionStrategy = _context.Database.CreateExecutionStrategy();

                try
                {
                    await executionStrategy.ExecuteAsync(async () =>
                    {
                        _context.Tickets.Add(ticket);
                        await using var transaction = await _context.Database.BeginTransactionAsync();

                        try
                        {
                            await _context.SaveChangesAsync();

                            var nowUtc = DateTime.UtcNow;
                            var requestedIds = sparePartInputs
                                .Select(x => x.PartId)
                                .Distinct()
                                .ToList();

                            var dbParts = await _context.MaintenanceInventoryParts
                                .Where(x => requestedIds.Contains(x.Id))
                                .ToDictionaryAsync(x => x.Id);

                            var dispatchRows = new List<TicketSparePartDispatch>();

                            foreach (var item in sparePartInputs)
                            {
                                if (!dbParts.TryGetValue(item.PartId, out var part) || !part.IsActive)
                                {
                                    stockValidationFailed = true;
                                    stockValidationMessage = "Hay articulos seleccionados que ya no estan disponibles.";
                                    break;
                                }

                                if (!part.Site.Equals(model.DispatchWarehouse, StringComparison.OrdinalIgnoreCase))
                                {
                                    stockValidationFailed = true;
                                    stockValidationMessage = $"El articulo {part.PartCode} - {part.PartName} no pertenece al almacen seleccionado.";
                                    break;
                                }

                                if (part.QuantityOnHand < item.Quantity)
                                {
                                    stockValidationFailed = true;
                                    stockValidationMessage = $"Stock insuficiente para {part.PartCode} - {part.PartName}. Disponible: {part.QuantityOnHand}.";
                                    break;
                                }

                                var totalCordoba = part.UnitCostCordoba.HasValue
                                    ? part.UnitCostCordoba.Value * item.Quantity
                                    : (decimal?)null;

                                var totalUsd = part.UnitCostUsd.HasValue
                                    ? part.UnitCostUsd.Value * item.Quantity
                                    : (decimal?)null;

                                part.QuantityOnHand -= item.Quantity;
                                part.QuantityIssued += item.Quantity;
                                part.LastIssueDate = nowUtc;
                                part.UpdatedAt = nowUtc;
                                ApplyMaintenanceInventoryStockStatusByQuantity(part);

                                dispatchRows.Add(new TicketSparePartDispatch
                                {
                                    TicketId = ticket.Id,
                                    MaintenanceInventoryPartId = part.Id,
                                    PartCode = part.PartCode,
                                    PartName = part.PartName,
                                    UnitOfMeasure = part.UnitOfMeasure,
                                    ItemCode = part.ItemCode,
                                    PartNumber = part.ManufacturerPartNumber,
                                    QuantityDispatched = item.Quantity,
                                    UnitCostCordoba = part.UnitCostCordoba,
                                    TotalCostCordoba = totalCordoba,
                                    UnitCostUsd = part.UnitCostUsd,
                                    TotalCostUsd = totalUsd,
                                    CreatedAt = nowUtc
                                });
                            }

                            if (stockValidationFailed)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return;
                            }

                            _context.TicketSparePartDispatches.AddRange(dispatchRows);
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });

                    if (stockValidationFailed)
                    {
                        ModelState.AddModelError(
                            SparePartsModelStateKey,
                            string.IsNullOrWhiteSpace(stockValidationMessage)
                                ? "No se pudo registrar la salida por un conflicto de inventario."
                                : stockValidationMessage);
                        break;
                    }

                    await NotifyNewTicketAsync(ticket);
                    return RedirectToAction(nameof(Index), new { department = "Inventario" });
                }
                catch (DbUpdateException) when (attempt < maxAttempts)
                {
                    _context.ChangeTracker.Clear();
                }
                catch (DbUpdateException)
                {
                    _context.ChangeTracker.Clear();
                    ModelState.AddModelError(string.Empty, "No se pudo guardar la salida de articulos por un conflicto de datos.");
                    break;
                }
            }

            await PopulateArticleOutputFormDataAsync(model, model.SparePartsJson);
            if (!ModelState.ContainsKey(SparePartsModelStateKey))
            {
                ModelState.AddModelError(string.Empty, "No fue posible crear la salida de articulos. Intente nuevamente.");
            }

            return View(model);
        }

        private async Task PopulateArticleOutputFormDataAsync(
            ArticleOutputCreateViewModel model,
            string? selectedSparePartsJson = null)
        {
            model.UserEmail = string.IsNullOrWhiteSpace(model.UserEmail)
                ? GetChangedBy()
                : model.UserEmail.Trim();

            var ticketSeed = new Ticket
            {
                RequestingUser = model.ReceivedBy ?? string.Empty,
                Site = model.DispatchWarehouse ?? string.Empty,
                Department = "Inventario",
                IncidentType = "Salida de articulos",
                Priority = PriorityLevel.Medium
            };

            await PopulateCreateFormDataAsync(ticketSeed);

            var warehouseSites = PurchaseRequestSites
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            if (!string.IsNullOrWhiteSpace(model.DispatchWarehouse) &&
                warehouseSites.All(x => !x.Equals(model.DispatchWarehouse, StringComparison.OrdinalIgnoreCase)))
            {
                model.DispatchWarehouse = string.Empty;
            }

            ViewBag.DispatchWarehouseOptions = warehouseSites
                .Select(x => new SelectListItem(
                    x,
                    x,
                    string.Equals(model.DispatchWarehouse, x, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var receivedByOptions = (ViewBag.RequestingUserOptions as IEnumerable<SelectListItem> ?? Enumerable.Empty<SelectListItem>())
                .Select(x => new SelectListItem(
                    x.Text,
                    x.Value,
                    string.Equals(model.ReceivedBy, x.Value, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (!string.IsNullOrWhiteSpace(model.ReceivedBy) &&
                receivedByOptions.All(x => !x.Value.Equals(model.ReceivedBy, StringComparison.OrdinalIgnoreCase)))
            {
                receivedByOptions.Insert(0, new SelectListItem(model.ReceivedBy, model.ReceivedBy, true));
            }

            ViewBag.ReceivedByOptions = receivedByOptions;

            ViewBag.ArticleOutputStatusOptions = ArticleOutputStatuses
                .Select(x => new SelectListItem(
                    x,
                    x,
                    string.Equals(model.DispatchStatus, x, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var partsCatalogRows = await _context.MaintenanceInventoryParts
                .AsNoTracking()
                .Where(x => x.IsActive && x.QuantityOnHand > 0)
                .OrderBy(x => x.PartName)
                .Select(x => new MaintenanceSparePartCatalogItem
                {
                    Id = x.Id,
                    Label = $"{x.PartCode} - {x.PartName}",
                    Item = x.ItemCode,
                    PartNumber = x.ManufacturerPartNumber,
                    Site = x.Site,
                    Tenencia = x.Tenencia,
                    UnitOfMeasure = x.UnitOfMeasure,
                    Stock = x.QuantityOnHand,
                    UnitCostCordoba = x.UnitCostCordoba,
                    UnitCostUsd = x.UnitCostUsd
                })
                .ToListAsync();

            partsCatalogRows = NormalizeLegacyCatalogIdentifiers(partsCatalogRows);

            var duplicateCostByPart = BuildCatalogDuplicateCostFallback(partsCatalogRows);
            var partIds = partsCatalogRows
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            var dispatchCostRows = await _context.TicketSparePartDispatches
                .AsNoTracking()
                .Where(x => partIds.Contains(x.MaintenanceInventoryPartId))
                .GroupBy(x => x.MaintenanceInventoryPartId)
                .Select(g => new
                {
                    PartId = g.Key,
                    UnitCostCordoba = g
                        .OrderByDescending(x => x.CreatedAt)
                        .Select(x => x.UnitCostCordoba)
                        .FirstOrDefault(),
                    UnitCostUsd = g
                        .OrderByDescending(x => x.CreatedAt)
                        .Select(x => x.UnitCostUsd)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var dispatchCostByPart = dispatchCostRows.ToDictionary(x => x.PartId);

            var partsCatalog = partsCatalogRows
                .Select(x =>
                {
                    dispatchCostByPart.TryGetValue(x.Id, out var fallbackCost);
                    duplicateCostByPart.TryGetValue(x.Id, out var duplicateFallback);
                    x.UnitCostCordoba = ResolveCatalogCost(x.UnitCostCordoba, duplicateFallback.Cordoba);
                    x.UnitCostUsd = ResolveCatalogCost(x.UnitCostUsd, duplicateFallback.Usd);
                    x.UnitCostCordoba = ResolveCatalogCost(x.UnitCostCordoba, fallbackCost?.UnitCostCordoba);
                    x.UnitCostUsd = ResolveCatalogCost(x.UnitCostUsd, fallbackCost?.UnitCostUsd);
                    return x;
                })
                .ToList();

            ViewBag.ArticleSparePartsCatalogJson = JsonSerializer.Serialize(partsCatalog);
            ViewBag.SelectedSparePartsJson = string.IsNullOrWhiteSpace(selectedSparePartsJson)
                ? (string.IsNullOrWhiteSpace(model.SparePartsJson) ? "[]" : model.SparePartsJson)
                : selectedSparePartsJson;
        }

        private static TicketStatus MapArticleOutputTicketStatus(string status)
        {
            if (status.Equals("ENTREGADO", StringComparison.OrdinalIgnoreCase))
            {
                return TicketStatus.Closed;
            }

            if (status.Equals("EN PROCESO", StringComparison.OrdinalIgnoreCase))
            {
                return TicketStatus.InProgress;
            }

            return TicketStatus.Open;
        }

        private static string NormalizeArticleOutputStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status)
                ? "PENDIENTE"
                : status.Trim().ToUpperInvariant();
        }

        private static decimal? ResolveCatalogCost(decimal? current, decimal? fallback)
        {
            if (current.HasValue && current.Value > 0m)
            {
                return current;
            }

            if (fallback.HasValue && fallback.Value > 0m)
            {
                return fallback;
            }

            return current ?? fallback;
        }

        private static Dictionary<int, (decimal? Cordoba, decimal? Usd)> BuildCatalogDuplicateCostFallback(
            IReadOnlyCollection<MaintenanceSparePartCatalogItem> rows)
        {
            var result = new Dictionary<int, (decimal? Cordoba, decimal? Usd)>();
            if (rows.Count == 0)
            {
                return result;
            }

            var grouped = rows
                .GroupBy(BuildCatalogCostKey)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToList();

            foreach (var group in grouped)
            {
                var donorCordoba = group
                    .Where(x => x.UnitCostCordoba.HasValue && x.UnitCostCordoba.Value > 0m)
                    .Select(x => x.UnitCostCordoba)
                    .FirstOrDefault();

                var donorUsd = group
                    .Where(x => x.UnitCostUsd.HasValue && x.UnitCostUsd.Value > 0m)
                    .Select(x => x.UnitCostUsd)
                    .FirstOrDefault();

                foreach (var row in group)
                {
                    result[row.Id] = (donorCordoba, donorUsd);
                }
            }

            return result;
        }

        private static string BuildCatalogCostKey(MaintenanceSparePartCatalogItem row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var label = NormalizeCatalogLookupValue(row.Label);
            var item = NormalizeCatalogLookupValue(row.Item);
            var partNumber = NormalizeCatalogLookupValue(row.PartNumber);
            var site = NormalizeCatalogLookupValue(row.Site);
            var tenencia = NormalizeCatalogLookupValue(row.Tenencia);

            if (string.IsNullOrWhiteSpace(label))
            {
                return string.Empty;
            }

            return $"{label}|{item}|{partNumber}|{site}|{tenencia}";
        }

        private static List<MaintenanceSparePartCatalogItem> NormalizeMaintenanceCatalogSites(
            List<MaintenanceSparePartCatalogItem> rows)
        {
            rows ??= new List<MaintenanceSparePartCatalogItem>();

            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                row.Site = NormalizeMaintenanceSiteForMatching(row.Site);
                row.Tenencia = string.IsNullOrWhiteSpace(row.Tenencia) ? string.Empty : row.Tenencia.Trim();
            }

            return rows;
        }

        private static string NormalizeMaintenanceSiteForMatching(string? site)
        {
            if (string.IsNullOrWhiteSpace(site))
            {
                return string.Empty;
            }

            return site.Trim().ToUpperInvariant() switch
            {
                "ACT" or "PCT" => "PCT",
                "ASB" or "PSB" => "PSB",
                "AOZ" or "POZ" => "POZ",
                _ => site.Trim().ToUpperInvariant()
            };
        }

        private static List<MaintenanceSparePartCatalogItem> NormalizeLegacyCatalogIdentifiers(
            List<MaintenanceSparePartCatalogItem> rows)
        {
            rows ??= new List<MaintenanceSparePartCatalogItem>();

            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.Item) && LooksLikeLegacyItemCode(row.PartNumber))
                {
                    row.Item = row.PartNumber;
                    row.PartNumber = null;
                }
            }

            return rows;
        }

        private static bool LooksLikeLegacyItemCode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            if (normalized.Length < 6)
            {
                return false;
            }

            return normalized.All(char.IsDigit);
        }

        private static string NormalizeCatalogLookupValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value
                .Trim()
                .Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }

            return sb.ToString();
        }

        private static string BuildArticleOutputDescription(
            ArticleOutputCreateViewModel model,
            IReadOnlyCollection<MaintenanceSparePartDispatchInput> sparePartInputs,
            IReadOnlyDictionary<int, MaintenanceInventoryPart> partsById)
        {
            var summary = new StringBuilder();
            summary.AppendLine("Salida de articulos de inventario");
            summary.AppendLine($"Fecha/hora: {model.DispatchDateTime:yyyy-MM-dd HH:mm}");
            summary.AppendLine($"N° requisa: {model.RequisitionNumber}");
            summary.AppendLine($"Equipo destino: {model.TargetEquipment}");
            summary.AppendLine($"Almacen despacho: {model.DispatchWarehouse}");
            summary.AppendLine($"Recibi: {model.ReceivedBy}");
            summary.AppendLine($"Estado: {model.DispatchStatus}");
            summary.AppendLine("Articulos despachados:");

            foreach (var item in sparePartInputs)
            {
                if (!partsById.TryGetValue(item.PartId, out var part))
                {
                    continue;
                }

                summary.AppendLine($"- {part.PartCode} - {part.PartName} x{item.Quantity} ({part.UnitOfMeasure})");
            }

            return TrimToMax(summary.ToString(), 2000);
        }

        private static string? BuildArticleOutputObservations(ArticleOutputCreateViewModel model)
        {
            var notes = new List<string>();

            if (!string.IsNullOrWhiteSpace(model.SignatureName))
            {
                notes.Add($"Firma: {model.SignatureName.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(model.UserEmail))
            {
                notes.Add($"Usuario registro: {model.UserEmail.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(model.AdditionalNotes))
            {
                notes.Add($"Notas: {model.AdditionalNotes.Trim()}");
            }

            if (notes.Count == 0)
            {
                return null;
            }

            return TrimToMax(string.Join(" | ", notes), 2000);
        }

        public async Task<IActionResult> CreateMaintenance(string? site = null, string? incidentType = null)
        {
            static string? NormalizePresetValue(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                return value.Trim();
            }

            var normalizedIncidentType = NormalizePresetValue(incidentType);
            if (string.IsNullOrWhiteSpace(normalizedIncidentType))
            {
                normalizedIncidentType = "Mantenimiento Correctivo";
            }

            var ticket = new Ticket
            {
                Status = TicketStatus.Open,
                Department = "Mantenimiento",
                Site = NormalizePresetValue(site) ?? string.Empty,
                IncidentType = normalizedIncidentType,
                Priority = PriorityLevel.Medium,
                MaintenanceStage = "Recibida"
            };

            await PopulateMaintenanceFormDataAsync(ticket, selectedMaintenanceUiMetaJson: "{}");
            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMaintenance(
            Ticket ticket,
            IFormFile? reportEvidenceFile,
            string? sparePartsJson,
            string? maintenanceUiMetaJson,
            string? submitAction)
        {
            var canSetPriority = CanSetPriority();
            var sparePartInputs = ParseMaintenanceSparePartInputs(sparePartsJson);
            var normalizedSparePartsJson = SerializeMaintenanceSparePartInputs(sparePartInputs);
            var maintenanceUiMetaInput = ParseMaintenanceOrderUiMetaInput(maintenanceUiMetaJson);
            var normalizedMaintenanceUiMetaJson = SerializeMaintenanceOrderUiMetaInput(maintenanceUiMetaInput);

            ticket.Status = TicketStatus.Open;
            ticket.Department = "Mantenimiento";

            if (!canSetPriority)
            {
                ticket.Priority = PriorityLevel.Medium;
            }

            if (string.IsNullOrWhiteSpace(ticket.IncidentType))
            {
                ticket.IncidentType = "Mantenimiento Correctivo";
            }

            ticket.Site = ticket.Site?.Trim() ?? string.Empty;
            ticket.Tenencia = string.IsNullOrWhiteSpace(ticket.Tenencia)
                ? null
                : ticket.Tenencia.Trim();
            ticket.UnitCode = string.IsNullOrWhiteSpace(ticket.UnitCode)
                ? null
                : ticket.UnitCode.Trim();
            ticket.FailureCategory = string.IsNullOrWhiteSpace(ticket.FailureCategory)
                ? null
                : ticket.FailureCategory.Trim();
            ticket.DamagedElement = string.IsNullOrWhiteSpace(ticket.DamagedElement)
                ? null
                : ticket.DamagedElement.Trim();
            var selectedTechnicianCategories = ParseMultiSelectFieldValues(ticket.TechnicianCategory);
            ticket.TechnicianCategory = JoinMultiSelectFieldValues(selectedTechnicianCategories);
            var selectedTechnicians = ParseMultiSelectFieldValues(ticket.AssignedTechnician);
            ticket.AssignedTechnician = JoinMultiSelectFieldValues(selectedTechnicians);
            if ((ticket.TechnicianCategory?.Length ?? 0) > 60)
            {
                ModelState.AddModelError(nameof(Ticket.TechnicianCategory), "Selecciona menos categorias: excede el limite permitido.");
            }
            if ((ticket.AssignedTechnician?.Length ?? 0) > 100)
            {
                ModelState.AddModelError(nameof(Ticket.AssignedTechnician), "Selecciona menos tecnicos: excede el limite permitido.");
            }
            ticket.MaintenanceStage = string.IsNullOrWhiteSpace(ticket.MaintenanceStage)
                ? "Recibida"
                : ticket.MaintenanceStage.Trim();
            ticket.CostApprovalNotes = string.IsNullOrWhiteSpace(ticket.CostApprovalNotes)
                ? null
                : ticket.CostApprovalNotes.Trim();
            ticket.CostCenterCode = NormalizeTicketCostCenterCode(ticket.CostCenterCode);
            ticket.LaborCostCordoba = maintenanceUiMetaInput.LaborCostCordoba;
            ticket.LaborCostUsd = maintenanceUiMetaInput.LaborCostUsd;
            ticket.ExternalCostCordoba = maintenanceUiMetaInput.ExternalCostCordoba;
            ticket.ExternalCostUsd = maintenanceUiMetaInput.ExternalCostUsd;

            if (selectedTechnicianCategories.Count == 0)
            {
                ModelState.AddModelError(nameof(Ticket.TechnicianCategory), "Debe seleccionar al menos una categoria tecnica.");
            }

            if (selectedTechnicians.Count == 0)
            {
                ModelState.AddModelError(nameof(Ticket.AssignedTechnician), "Debe asignar al menos un tecnico responsable.");
            }
            else if (selectedTechnicianCategories.Count > 0)
            {
                var selectedTechnicianRows = await _context.MaintenanceTechnicians
                    .AsNoTracking()
                    .Where(x => x.IsActive && selectedTechnicians.Contains(x.FullName))
                    .ToListAsync();

                var selectedTechniciansByName = selectedTechnicianRows.ToDictionary(
                    x => x.FullName,
                    x => x,
                    StringComparer.OrdinalIgnoreCase);
                var activeOrdersByTech = await GetMaintenanceActiveOrdersByTechnicianAsync();

                foreach (var technicianName in selectedTechnicians)
                {
                    if (!selectedTechniciansByName.TryGetValue(technicianName, out var selectedTechnician))
                    {
                        ModelState.AddModelError(
                            nameof(Ticket.AssignedTechnician),
                            $"El tecnico '{technicianName}' no existe o esta inactivo.");
                        continue;
                    }

                    if (!selectedTechnicianCategories.Any(x =>
                            x.Equals(selectedTechnician.Category, StringComparison.OrdinalIgnoreCase)))
                    {
                        ModelState.AddModelError(
                            nameof(Ticket.AssignedTechnician),
                            $"El tecnico '{technicianName}' no coincide con las categorias seleccionadas.");
                    }

                    if (!selectedTechnician.IsAvailable)
                    {
                        ModelState.AddModelError(
                            nameof(Ticket.AssignedTechnician),
                            $"El tecnico '{technicianName}' no esta disponible.");
                    }

                    var activeOrders = activeOrdersByTech.TryGetValue(technicianName, out var count)
                        ? count
                        : 0;
                    if (activeOrders >= Math.Max(1, selectedTechnician.MaxActiveOrders))
                    {
                        ModelState.AddModelError(
                            nameof(Ticket.AssignedTechnician),
                            $"El tecnico '{technicianName}' ya alcanzo su capacidad maxima ({selectedTechnician.MaxActiveOrders} ordenes activas).");
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(ticket.UnitCode))
            {
                ModelState.AddModelError(nameof(Ticket.UnitCode), "Debe indicar la unidad o equipo a atender.");
            }

            if (!MaintenanceSites.Contains(ticket.Site, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(Ticket.Site), "Seleccione un sitio valido para mantenimiento.");
            }
            else
            {
                ticket.Site = MaintenanceSites
                    .First(site => site.Equals(ticket.Site, StringComparison.OrdinalIgnoreCase));
            }

            if (string.IsNullOrWhiteSpace(ticket.Tenencia) ||
                !MaintenanceTenencias.Contains(ticket.Tenencia, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(Ticket.Tenencia), "Seleccione la tenencia (Propio o Agregado).");
            }
            else
            {
                ticket.Tenencia = MaintenanceTenencias
                    .First(value => value.Equals(ticket.Tenencia, StringComparison.OrdinalIgnoreCase));
            }

            if (string.IsNullOrWhiteSpace(ticket.MaintenanceStage) ||
                !MaintenanceStageOptions.Contains(ticket.MaintenanceStage, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(Ticket.MaintenanceStage), "Seleccione una etapa valida del taller.");
            }
            else
            {
                ticket.MaintenanceStage = MaintenanceStageOptions
                    .First(value => value.Equals(ticket.MaintenanceStage, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(ticket.FailureCategory))
            {
                if (!MaintenanceFailureCategories.Contains(ticket.FailureCategory, StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(Ticket.FailureCategory), "Seleccione una categoria de falla valida.");
                }
                else
                {
                    ticket.FailureCategory = MaintenanceFailureCategories
                        .First(value => value.Equals(ticket.FailureCategory, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (ticket.ExitOdometerKm.HasValue &&
                ticket.FailureOdometerKm.HasValue &&
                ticket.ExitOdometerKm.Value < ticket.FailureOdometerKm.Value)
            {
                ModelState.AddModelError(nameof(Ticket.ExitOdometerKm), "El KM de salida no puede ser menor al KM de falla.");
            }

            NormalizeTechnicalFieldsForRole(ticket, canEditSolutionForm: false);
            NormalizeComponentChangeFields(ticket);

            var selectedPartsById = new Dictionary<int, MaintenanceInventoryPart>();
            if (sparePartInputs.Count > 0)
            {
                var requestedIds = sparePartInputs
                    .Select(x => x.PartId)
                    .Distinct()
                    .ToList();

                selectedPartsById = await _context.MaintenanceInventoryParts
                    .AsNoTracking()
                    .Where(x => x.IsActive && requestedIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                foreach (var item in sparePartInputs)
                {
                    if (!selectedPartsById.TryGetValue(item.PartId, out var part))
                    {
                        ModelState.AddModelError(SparePartsModelStateKey, "Hay repuestos seleccionados que ya no estan disponibles.");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(ticket.Site) &&
                        !NormalizeMaintenanceSiteForMatching(part.Site).Equals(
                            NormalizeMaintenanceSiteForMatching(ticket.Site),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(
                            SparePartsModelStateKey,
                            $"El repuesto {part.PartCode} - {part.PartName} no pertenece al sitio seleccionado.");
                    }

                    if (!string.IsNullOrWhiteSpace(ticket.Tenencia) &&
                        !part.Tenencia.Equals(ticket.Tenencia, StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(
                            SparePartsModelStateKey,
                            $"El repuesto {part.PartCode} - {part.PartName} no coincide con la tenencia seleccionada.");
                    }

                    if (part.QuantityOnHand < item.Quantity)
                    {
                        ModelState.AddModelError(
                            SparePartsModelStateKey,
                            $"Stock insuficiente para {part.PartCode} - {part.PartName}. Disponible: {part.QuantityOnHand}.");
                    }
                }

                ticket.SparePartRequired = true;
                ticket.SparePartPurchased = false;
                ticket.SparePartDetails = string.Join(", ",
                    sparePartInputs
                        .Where(x => selectedPartsById.ContainsKey(x.PartId))
                        .Select(x =>
                        {
                            var part = selectedPartsById[x.PartId];
                            return $"{part.PartCode} {part.PartName} x{x.Quantity}";
                        }));
            }
            else
            {
                ticket.SparePartRequired = false;
                ticket.SparePartPurchased = false;
                ticket.SparePartDetails = null;
            }

            var estimatedSpareCordoba = sparePartInputs
                .Where(x => selectedPartsById.ContainsKey(x.PartId) && selectedPartsById[x.PartId].UnitCostCordoba.HasValue)
                .Sum(x => selectedPartsById[x.PartId].UnitCostCordoba!.Value * x.Quantity);
            var estimatedSpareUsd = sparePartInputs
                .Where(x => selectedPartsById.ContainsKey(x.PartId) && selectedPartsById[x.PartId].UnitCostUsd.HasValue)
                .Sum(x => selectedPartsById[x.PartId].UnitCostUsd!.Value * x.Quantity);

            var estimatedTotalCordoba = estimatedSpareCordoba +
                                        (ticket.ChangedComponentCostCordoba ?? 0m) +
                                        (ticket.LaborCostCordoba ?? 0m) +
                                        (ticket.ExternalCostCordoba ?? 0m);
            var estimatedTotalUsd = estimatedSpareUsd +
                                    (ticket.ChangedComponentCostUsd ?? 0m) +
                                    (ticket.LaborCostUsd ?? 0m) +
                                    (ticket.ExternalCostUsd ?? 0m);

            var requiresCostApproval = estimatedTotalCordoba >= MaintenanceCostApprovalThresholdCordoba ||
                                       estimatedTotalUsd >= MaintenanceCostApprovalThresholdUsd;
            ticket.RequiresCostApproval = requiresCostApproval;
            ticket.CostApproved = !requiresCostApproval;
            ticket.CostApprovedBy = ticket.CostApproved ? GetChangedBy() : null;
            ticket.CostApprovedAtUtc = ticket.CostApproved ? DateTime.UtcNow : null;

            NormalizeSparePartFields(ticket);
            ValidateSolutionBusinessRules(ticket, null, afterEvidenceFile: null);
            await ValidateTicketCostCenterTraceabilityAsync(
                ticket,
                hasSpending: HasAnyStructuredCost(ticket) || sparePartInputs.Count > 0);

            if (!ModelState.IsValid)
            {
                await PopulateMaintenanceFormDataAsync(ticket, normalizedSparePartsJson, normalizedMaintenanceUiMetaJson);
                return View(ticket);
            }

            ticket.AttachmentPath = await SaveUploadedFileAsync(
                reportEvidenceFile,
                ticket.AttachmentPath,
                new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" });

            ticket.CreatedDate = DateTime.UtcNow;
            ticket.SLADeadline = CalculateSLA(ticket.Priority);
            ticket.ClosedDate = null;

            const int maxAttempts = 3;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ticket.TicketNumber = await GenerateTicketNumberAsync();
                var stockChanged = false;
                var executionStrategy = _context.Database.CreateExecutionStrategy();

                try
                {
                    await executionStrategy.ExecuteAsync(async () =>
                    {
                        _context.Tickets.Add(ticket);
                        await using var transaction = await _context.Database.BeginTransactionAsync();

                        try
                        {
                            await _context.SaveChangesAsync();

                            if (sparePartInputs.Count > 0)
                            {
                                var nowUtc = DateTime.UtcNow;
                                var requestedIds = sparePartInputs
                                    .Select(x => x.PartId)
                                    .Distinct()
                                    .ToList();

                                var dbParts = await _context.MaintenanceInventoryParts
                                    .Where(x => requestedIds.Contains(x.Id))
                                    .ToDictionaryAsync(x => x.Id);

                                var dispatchRows = new List<TicketSparePartDispatch>();

                                foreach (var item in sparePartInputs)
                                {
                                    if (!dbParts.TryGetValue(item.PartId, out var part) || !part.IsActive)
                                    {
                                        ModelState.AddModelError(SparePartsModelStateKey, "Hay repuestos seleccionados que ya no estan disponibles.");
                                        stockChanged = true;
                                        break;
                                    }

                                    if (part.QuantityOnHand < item.Quantity)
                                    {
                                        ModelState.AddModelError(
                                            SparePartsModelStateKey,
                                            $"Stock insuficiente para {part.PartCode} - {part.PartName}. Disponible: {part.QuantityOnHand}.");
                                        stockChanged = true;
                                        break;
                                    }

                                    var totalCordoba = part.UnitCostCordoba.HasValue
                                        ? part.UnitCostCordoba.Value * item.Quantity
                                        : (decimal?)null;

                                    var totalUsd = part.UnitCostUsd.HasValue
                                        ? part.UnitCostUsd.Value * item.Quantity
                                        : (decimal?)null;

                                    part.QuantityOnHand -= item.Quantity;
                                    part.QuantityIssued += item.Quantity;
                                    part.LastIssueDate = nowUtc;
                                    part.UpdatedAt = nowUtc;
                                    ApplyMaintenanceInventoryStockStatusByQuantity(part);

                                    dispatchRows.Add(new TicketSparePartDispatch
                                    {
                                        TicketId = ticket.Id,
                                        MaintenanceInventoryPartId = part.Id,
                                        PartCode = part.PartCode,
                                        PartName = part.PartName,
                                        UnitOfMeasure = part.UnitOfMeasure,
                                        ItemCode = part.ItemCode,
                                        PartNumber = part.ManufacturerPartNumber,
                                        QuantityDispatched = item.Quantity,
                                        UnitCostCordoba = part.UnitCostCordoba,
                                        TotalCostCordoba = totalCordoba,
                                        UnitCostUsd = part.UnitCostUsd,
                                        TotalCostUsd = totalUsd,
                                        CreatedAt = nowUtc
                                    });
                                }

                                if (stockChanged)
                                {
                                    await transaction.RollbackAsync();
                                    _context.ChangeTracker.Clear();
                                    return;
                                }

                                _context.TicketSparePartDispatches.AddRange(dispatchRows);
                                await _context.SaveChangesAsync();
                            }

                            await transaction.CommitAsync();
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });

                    if (stockChanged)
                    {
                        break;
                    }

                    try
                    {
                        var creationMessage = string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase)
                            ? "Orden de mantenimiento creada"
                            : "Ticket creado";
                        _context.TicketHistories.Add(CreateHistory(
                            ticket.Id,
                            "Creacion",
                            null,
                            creationMessage,
                            BuildQuickAuditActor(GetChangedBy())));
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo registrar auditoria de creacion para orden de mantenimiento {TicketId}.", ticket.Id);
                    }

                    await NotifyNewTicketAsync(ticket);
                    if (ticket.RequiresCostApproval && !ticket.CostApproved)
                    {
                        await NotifyMaintenanceCostApprovalRequiredAsync(ticket);
                        await RegisterMaintenanceAlertHistoryAsync(ticket.Id, MaintenanceAlertCostApprovalHistoryKey, "Pendiente", "Alerta de aprobacion de costos enviada");
                    }

                    if (string.Equals(submitAction, "save_pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        return RedirectToAction(nameof(DownloadPdf), new
                        {
                            id = ticket.Id,
                            department = "Mantenimiento"
                        });
                    }

                    TempData["TicketsMessage"] = $"Orden {ticket.TicketNumber} creada correctamente.";
                    return RedirectToAction(nameof(Index), new
                    {
                        department = "Mantenimiento",
                        search = ticket.TicketNumber,
                        maintenanceView = "detail",
                        page = 1,
                        pageSize = 25
                    });
                }
                catch (DbUpdateException) when (attempt < maxAttempts)
                {
                    _context.ChangeTracker.Clear();
                }
                catch (DbUpdateException)
                {
                    _context.ChangeTracker.Clear();
                    ModelState.AddModelError(string.Empty, "No se pudo guardar la orden de mantenimiento por un conflicto de datos.");
                    break;
                }
            }

            await PopulateMaintenanceFormDataAsync(ticket, normalizedSparePartsJson, normalizedMaintenanceUiMetaJson);
            if (!ModelState.ContainsKey(SparePartsModelStateKey))
            {
                ModelState.AddModelError(string.Empty, "No fue posible crear la orden de mantenimiento. Intente nuevamente.");
            }
            return View(ticket);
        }

        public async Task<IActionResult> CreateFinance(string? site = null, string? requestType = null, string? ocStage = null, int? sourceOcTicketId = null)
        {
            var normalizedRequestType = string.IsNullOrWhiteSpace(requestType) ? string.Empty : requestType.Trim();
            var normalizedOcStage = NormalizeOcWorkflowStage(ocStage);
            var isOcRequestType = IsPurchaseOrderRequestType(normalizedRequestType);
            if (!isOcRequestType)
            {
                normalizedOcStage = "request";
                sourceOcTicketId = null;
            }
            else
            {
                normalizedRequestType = normalizedOcStage == "order"
                    ? "Orden de compra"
                    : "Solicitud de OC";
            }

            var model = new FinanceRequestCreateViewModel
            {
                Site = string.IsNullOrWhiteSpace(site) ? string.Empty : site.Trim(),
                RequestType = normalizedRequestType,
                OcStatus = "PENDIENTE",
                NeededByDate = DateTime.Today.AddDays(2),
                OcWorkflowStage = normalizedOcStage,
                SourceOcTicketId = sourceOcTicketId
            };

            if (isOcRequestType && normalizedOcStage == "order" && sourceOcTicketId.HasValue)
            {
                var sourceTicket = await _context.Tickets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t =>
                        t.Id == sourceOcTicketId.Value &&
                        t.Department == "Inventario" &&
                        t.IncidentType == "Solicitud de OC");

                if (sourceTicket != null)
                {
                    var sourceMeta = ParseFinanceTicketMeta(sourceTicket);
                    model.RequestingUser = sourceTicket.RequestingUser ?? string.Empty;
                    model.Site = sourceTicket.Site ?? model.Site;
                    model.NeededByDate = sourceMeta.NeededByDate ?? model.NeededByDate;
                    model.OcUsage = sourceMeta.OcUsage;
                    model.OcRequirementJustification = sourceMeta.OcRequirementJustification;
                    model.OcQuantityJustification = sourceMeta.OcQuantityJustification;
                    model.OcLineCount = sourceMeta.OcLineCount;
                    model.OcItemDescription = sourceMeta.OcItemDescription;
                    model.OcPurchaseOrderNumber = sourceMeta.OcPurchaseOrderNumber;
                    model.ReferenceNumber = sourceMeta.OcPurchaseOrderNumber;
                }
            }

            if (!CanSetPriority())
            {
                model.Priority = PriorityLevel.Medium;
            }

            await PopulateFinanceFormDataAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFinance(
            FinanceRequestCreateViewModel model,
            IFormFile? supportDocumentFile)
        {
            var canSetPriority = CanSetPriority();
            if (!canSetPriority)
            {
                model.Priority = PriorityLevel.Medium;
            }

            model.RequestingUser = model.RequestingUser?.Trim() ?? string.Empty;
            model.Site = model.Site?.Trim() ?? string.Empty;
            model.RequestType = model.RequestType?.Trim() ?? string.Empty;
            model.Currency = model.Currency?.Trim() ?? "C$";
            model.CostCenter = model.CostCenter?.Trim() ?? string.Empty;
            model.VendorName = string.IsNullOrWhiteSpace(model.VendorName) ? null : model.VendorName.Trim();
            model.ReferenceNumber = string.IsNullOrWhiteSpace(model.ReferenceNumber) ? null : model.ReferenceNumber.Trim();
            model.BeneficiaryDocument = string.IsNullOrWhiteSpace(model.BeneficiaryDocument) ? null : model.BeneficiaryDocument.Trim();
            model.PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? null : model.PaymentMethod.Trim();
            model.BankAccount = string.IsNullOrWhiteSpace(model.BankAccount) ? null : model.BankAccount.Trim();
            model.OcUsage = string.IsNullOrWhiteSpace(model.OcUsage) ? null : model.OcUsage.Trim();
            model.OcRequirementJustification = string.IsNullOrWhiteSpace(model.OcRequirementJustification) ? null : model.OcRequirementJustification.Trim();
            model.OcQuantityJustification = string.IsNullOrWhiteSpace(model.OcQuantityJustification) ? null : model.OcQuantityJustification.Trim();
            model.OcStatus = string.IsNullOrWhiteSpace(model.OcStatus) ? "PENDIENTE" : model.OcStatus.Trim().ToUpperInvariant();
            model.OcItemDescription = string.IsNullOrWhiteSpace(model.OcItemDescription) ? null : model.OcItemDescription.Trim();
            model.OcPurchaseOrderNumber = string.IsNullOrWhiteSpace(model.OcPurchaseOrderNumber) ? null : model.OcPurchaseOrderNumber.Trim();
            model.BusinessJustification = model.BusinessJustification?.Trim() ?? string.Empty;
            model.AdditionalNotes = string.IsNullOrWhiteSpace(model.AdditionalNotes) ? null : model.AdditionalNotes.Trim();
            model.OcWorkflowStage = NormalizeOcWorkflowStage(model.OcWorkflowStage);
            var isOcRequest = IsPurchaseOrderRequestType(model.RequestType);
            var isOcOrderStage = isOcRequest && model.OcWorkflowStage == "order";

            if (isOcRequest)
            {
                model.RequestType = isOcOrderStage
                    ? "Orden de compra"
                    : "Solicitud de OC";

                if (isOcOrderStage)
                {
                    if (!model.SourceOcTicketId.HasValue || model.SourceOcTicketId.Value <= 0)
                    {
                        ModelState.AddModelError(nameof(model.SourceOcTicketId), "Debes indicar la solicitud de OC origen.");
                    }
                    else
                    {
                        var sourceOcTicket = await _context.Tickets
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t =>
                                t.Id == model.SourceOcTicketId.Value &&
                                t.Department == "Inventario" &&
                                t.IncidentType == "Solicitud de OC");

                        if (sourceOcTicket == null)
                        {
                            ModelState.AddModelError(nameof(model.SourceOcTicketId), "No se encontró la solicitud de OC base para continuar con la orden.");
                        }
                        else
                        {
                            var sourceMeta = ParseFinanceTicketMeta(sourceOcTicket);
                            model.RequestingUser = sourceOcTicket.RequestingUser?.Trim() ?? model.RequestingUser;
                            model.Site = sourceOcTicket.Site?.Trim() ?? model.Site;
                            model.NeededByDate = sourceMeta.NeededByDate ?? model.NeededByDate;
                            model.OcUsage = sourceMeta.OcUsage ?? model.OcUsage;
                            model.OcRequirementJustification = sourceMeta.OcRequirementJustification ?? model.OcRequirementJustification;
                            model.OcQuantityJustification = sourceMeta.OcQuantityJustification ?? model.OcQuantityJustification;
                            model.OcItemDescription = sourceMeta.OcItemDescription ?? model.OcItemDescription;
                            model.OcLineCount = sourceMeta.OcLineCount > 0 ? sourceMeta.OcLineCount : model.OcLineCount;
                        }
                    }
                }
                else
                {
                    model.SourceOcTicketId = null;
                    model.OcPurchaseOrderNumber = null;
                }

                if (!PurchaseRequestSites.Contains(model.Site, StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(model.Site), "Selecciona un sitio valido: ACT, ASB, AOZ, ACH o ALRUS.");
                }

                model.Amount = model.Amount > 0m ? model.Amount : 0.01m;
                model.Currency = "C$";
                model.CostCenter = "OC-TRAWZACONS";
                model.PaymentMethod = "N/A";
                model.BankAccount = null;
                model.VendorName = null;
                model.BeneficiaryDocument = null;
                model.ReferenceNumber = string.IsNullOrWhiteSpace(model.ReferenceNumber)
                    ? (isOcOrderStage ? model.OcPurchaseOrderNumber : null)
                    : model.ReferenceNumber;
                model.Priority = PriorityLevel.Medium;
                model.BusinessJustification = string.IsNullOrWhiteSpace(model.OcRequirementJustification)
                    ? "Solicitud de OC operativa para inventario."
                    : model.OcRequirementJustification;

                ModelState.Remove(nameof(model.Amount));
                ModelState.Remove(nameof(model.Currency));
                ModelState.Remove(nameof(model.CostCenter));
                ModelState.Remove(nameof(model.Priority));
                ModelState.Remove(nameof(model.PaymentMethod));
                ModelState.Remove(nameof(model.BankAccount));
                ModelState.Remove(nameof(model.VendorName));
                ModelState.Remove(nameof(model.BeneficiaryDocument));
                ModelState.Remove(nameof(model.BusinessJustification));
                ModelState.Remove("supportDocumentFile");
            }
            else
            {
                var submittedSite = model.Site?.Trim() ?? string.Empty;
                var isLegacyLimonAlias = string.Equals(submittedSite, "Mina El Limon", StringComparison.OrdinalIgnoreCase);

                if (!FinanceRequestSites.Contains(submittedSite, StringComparer.OrdinalIgnoreCase) && !isLegacyLimonAlias)
                {
                    ModelState.AddModelError(
                        nameof(model.Site),
                        "Selecciona un sitio valido: Plantel San Benito, Mina El Limón, Mina La Libertad, Oficinas Centrales TW o Comprador.");
                }
                else
                {
                    model.Site = isLegacyLimonAlias
                        ? "Mina El Limón"
                        : FinanceRequestSites.First(site => site.Equals(submittedSite, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (model.NeededByDate.Date < DateTime.Today)
            {
                ModelState.AddModelError(nameof(model.NeededByDate), "La fecha requerida no puede ser anterior a hoy.");
            }

            ValidateFinanceRequestByType(model, supportDocumentFile);

            int requiredApprovals;
            FinanceBudgetSnapshot budgetSnapshot;
            if (!isOcRequest)
            {
                var activeCostCenters = await GetActiveFinanceCostCentersAsync();
                var selectedCostCenter = FindFinanceCostCenter(activeCostCenters, model.CostCenter);
                if (selectedCostCenter != null)
                {
                    model.CostCenter = selectedCostCenter.CostCenterNumber;
                }
                else if (activeCostCenters.Count > 0)
                {
                    ModelState.AddModelError(nameof(model.CostCenter), "Debes seleccionar un #CC valido del catalogo.");
                }

                var approvalPolicy = await GetFinanceApprovalPolicyAsync(
                    model.RequestType,
                    model.CostCenter,
                    model.Amount,
                    model.Currency);
                requiredApprovals = approvalPolicy.RequiredApprovals;
                budgetSnapshot = await BuildFinanceBudgetSnapshotAsync(
                    model.CostCenter,
                    model.Amount,
                    model.Currency,
                    DateTime.UtcNow);

                if (budgetSnapshot.ExceedsAfterRequest && budgetSnapshot.RequireAuthorizationOnOverrun)
                {
                    requiredApprovals = Math.Clamp(Math.Max(requiredApprovals + 1, 2), 1, 5);
                }

                if (budgetSnapshot.ExceedsAfterRequest && budgetSnapshot.HardStopOnOverrun)
                {
                    ModelState.AddModelError(
                        nameof(model.Amount),
                        $"El monto supera el presupuesto disponible del centro de costo. Disponible actual C$: {budgetSnapshot.RemainingCordoba:N2}.");
                }
            }
            else
            {
                requiredApprovals = 0;
                var now = DateTime.UtcNow;
                budgetSnapshot = new FinanceBudgetSnapshot
                {
                    LimitCordoba = model.Amount,
                    CommittedCordoba = 0m,
                    RemainingCordoba = model.Amount,
                    ExceedsAfterRequest = false,
                    HardStopOnOverrun = false,
                    RequireAuthorizationOnOverrun = false,
                    AlertThresholdPercent = 100,
                    CostCenterNumber = model.CostCenter,
                    CostCenterName = "Solicitud OC",
                    Area = model.Site,
                    RequestedCordoba = model.Amount,
                    UsagePercentAfterRequest = 0d,
                    IsNearThresholdAfterRequest = false,
                    Year = now.Year,
                    Month = now.Month
                };
            }

            if (!ModelState.IsValid)
            {
                await PopulateFinanceFormDataAsync(model);
                return View(model);
            }

            var ticket = new Ticket
            {
                RequestingUser = model.RequestingUser.Trim(),
                Site = model.Site.Trim(),
                Department = isOcRequest ? "Inventario" : "Finanzas",
                CostCenterCode = model.CostCenter,
                IncidentType = model.RequestType.Trim(),
                Priority = model.Priority,
                Status = TicketStatus.Open,
                AssignedTechnician = null,
                Description = BuildFinanceDescription(model, requiredApprovals, budgetSnapshot),
                Observations = BuildFinanceObservations(model, budgetSnapshot),
                RequiresCostApproval = requiredApprovals > 0,
                CostApproved = false,
                CostApprovedBy = null,
                CostApprovedAtUtc = null,
                CostApprovalNotes = budgetSnapshot.ExceedsAfterRequest && budgetSnapshot.RequireAuthorizationOnOverrun
                    ? "Sobregiro detectado. Requiere autorizacion adicional."
                    : null,
                FinanceReconciliationStatus = "Pendiente",
                FinanceReturnedForCorrection = false,
                FinanceReturnedAtUtc = null,
                FinanceReturnedBy = null,
                FinanceReturnReason = null,
                FinanceEscalatedAtUtc = null,
                FinanceEscalatedTo = null
            };

            if (!isOcRequest)
            {
                ticket.AttachmentPath = await SaveUploadedFileAsync(
                    supportDocumentFile,
                    ticket.AttachmentPath,
                    new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx", ".xls", ".xlsx" });
            }

            ticket.CreatedDate = DateTime.UtcNow;
            ticket.SLADeadline = CalculateSLA(ticket.Priority);
            ticket.ClosedDate = null;

            const int maxAttempts = 3;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ticket.TicketNumber = await GenerateTicketNumberAsync();
                _context.Tickets.Add(ticket);

                try
                {
                    await _context.SaveChangesAsync();
                    await NotifyNewTicketAsync(ticket);
                    if (!isOcRequest)
                    {
                        await RegisterFinanceAlertHistoryAsync(
                            ticket.Id,
                            FinanceAlertPendingApprovalHistoryKey,
                            "Pendiente",
                            $"Solicitud creada con {requiredApprovals} aprobación(es) requerida(s).");
                    }

                    if (isOcRequest)
                    {
                        if (!isOcOrderStage)
                        {
                            return RedirectToAction(
                                nameof(CreateFinance),
                                new
                                {
                                    requestType = "Orden de compra",
                                    ocStage = "order",
                                    sourceOcTicketId = ticket.Id
                                });
                        }

                        return RedirectToAction(nameof(Index), new { department = "Inventario" });
                    }

                    return RedirectToAction(nameof(Index), new { department = "Finanzas" });
                }
                catch (DbUpdateException) when (attempt < maxAttempts)
                {
                    _context.Entry(ticket).State = EntityState.Detached;
                }
            }

            await PopulateFinanceFormDataAsync(model);
            ModelState.AddModelError(string.Empty, isOcRequest
                ? (isOcOrderStage
                    ? "No fue posible crear la orden de compra. Intente nuevamente."
                    : "No fue posible crear la solicitud de OC. Intente nuevamente.")
                : "No fue posible crear la solicitud financiera. Intente nuevamente.");
            return View(model);
        }

        public async Task<IActionResult> Create(string? department = null, string? site = null, string? incidentType = null)
        {
            static string? NormalizePresetValue(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                return value.Trim();
            }

            var normalizedDepartment = NormalizePresetValue(department);
            var normalizedIncidentType = NormalizePresetValue(incidentType);

            if (normalizedDepartment != null &&
                normalizedDepartment.Equals("Finanzas", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(CreateFinance), new
                {
                    site = NormalizePresetValue(site),
                    requestType = normalizedIncidentType
                });
            }

            if (normalizedDepartment != null &&
                normalizedDepartment.Equals("Mantenimiento", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(normalizedIncidentType))
            {
                normalizedIncidentType = "Mantenimiento Correctivo";
            }

            var ticket = new Ticket
            {
                Status = TicketStatus.Open,
                Department = normalizedDepartment ?? string.Empty,
                Site = NormalizePresetValue(site) ?? string.Empty,
                IncidentType = normalizedIncidentType ?? string.Empty
            };

            await PopulateCreateFormDataAsync(ticket);
            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Ticket ticket,
            IFormFile? reportEvidenceFile,
            IFormFile? beforeEvidenceFile,
            IFormFile? afterEvidenceFile,
            IFormFile? technicalSheetFile,
            IFormFile? exitOrderFile)
        {
            var canEditSolutionForm = CanEditSolutionForm();
            var canSetPriority = CanSetPriority();

            ticket.Status = TicketStatus.Open;
            if (!canSetPriority)
            {
                ticket.Priority = PriorityLevel.Medium;
            }

            NormalizeTechnicalFieldsForRole(ticket, canEditSolutionForm);
            NormalizeSparePartFields(ticket);
            NormalizeComponentChangeFields(ticket);
            ticket.CostCenterCode = NormalizeTicketCostCenterCode(ticket.CostCenterCode);
            ValidateSolutionBusinessRules(ticket, null, afterEvidenceFile);
            await ValidateTicketCostCenterTraceabilityAsync(
                ticket,
                hasSpending: HasAnyStructuredCost(ticket));

            if (!ModelState.IsValid)
            {
                await PopulateCreateFormDataAsync(ticket);
                return View(ticket);
            }

            ticket.AttachmentPath = await SaveUploadedFileAsync(reportEvidenceFile, ticket.AttachmentPath, new[] { ".jpg", ".jpeg", ".png", ".webp" });

            if (canEditSolutionForm)
            {
                ticket.BeforeEvidencePath = await SaveUploadedFileAsync(beforeEvidenceFile, ticket.BeforeEvidencePath);
                ticket.AfterEvidencePath = await SaveUploadedFileAsync(afterEvidenceFile, ticket.AfterEvidencePath);
                ticket.TechnicalSheetPath = await SaveUploadedFileAsync(technicalSheetFile, ticket.TechnicalSheetPath, new[] { ".jpg", ".jpeg", ".png", ".webp" });
                ticket.ExitOrderPath = await SaveUploadedFileAsync(exitOrderFile, ticket.ExitOrderPath, new[] { ".jpg", ".jpeg", ".png", ".webp" });
            }
            else
            {
                ticket.BeforeEvidencePath = null;
                ticket.AfterEvidencePath = null;
                ticket.TechnicalSheetPath = null;
                ticket.ExitOrderPath = null;
                ticket.Observations = null;
            }

            ticket.CreatedDate = DateTime.UtcNow;
            ticket.SLADeadline = CalculateSLA(ticket.Priority);
            ticket.ClosedDate = ticket.Status == TicketStatus.Closed ? DateTime.UtcNow : null;

            const int maxAttempts = 3;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ticket.TicketNumber = await GenerateTicketNumberAsync();
                _context.Tickets.Add(ticket);

                try
                {
                    await _context.SaveChangesAsync();

                    try
                    {
                        var creationMessage = string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase)
                            ? "Orden de mantenimiento creada"
                            : "Ticket creado";
                        _context.TicketHistories.Add(CreateHistory(
                            ticket.Id,
                            "Creacion",
                            null,
                            creationMessage,
                            BuildQuickAuditActor(GetChangedBy())));
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo registrar auditoria de creacion para ticket {TicketId}.", ticket.Id);
                    }

                    await NotifyNewTicketAsync(ticket);
                    if (string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase))
                    {
                        TempData["TicketsMessage"] = $"Orden {ticket.TicketNumber} creada correctamente.";
                        return RedirectToAction(nameof(Index), new
                        {
                            department = "Mantenimiento",
                            search = ticket.TicketNumber,
                            maintenanceView = "detail",
                            page = 1,
                            pageSize = 25
                        });
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException) when (attempt < maxAttempts)
                {
                    _context.Entry(ticket).State = EntityState.Detached;
                }
            }

            await PopulateCreateFormDataAsync(ticket);
            ModelState.AddModelError(string.Empty, "No fue posible crear el ticket. Intente nuevamente.");
            return View(ticket);
        }

        [Authorize(Roles = "Administrator,Technician,CoordinadorIT")]
        public async Task<IActionResult> Edit(int? id, string? department = null)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket == null)
            {
                return NotFound();
            }

            ViewBag.ReturnDepartment = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            if (string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase))
            {
                await LoadTicketSparePartDispatchesAsync(ticket);
                await PopulateMaintenanceFormDataAsync(ticket, "[]");
            }
            else
            {
                await PopulateCreateFormDataAsync(ticket);
            }
            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Technician,CoordinadorIT")]
        public async Task<IActionResult> Edit(
            int id,
            Ticket ticket,
            IFormFile? attachmentFile,
            IFormFile? beforeEvidenceFile,
            IFormFile? afterEvidenceFile,
            IFormFile? technicalSheetFile,
            IFormFile? exitOrderFile,
            string? sparePartsJson,
            string? submitAction)
        {
            if (id != ticket.Id)
            {
                return NotFound();
            }

            var normalizedSparePartsJson = "[]";

            if (ticket.RowVersion == null)
            {
                ModelState.AddModelError(string.Empty, "No se pudo validar concurrencia del ticket.");
                ViewBag.ReturnDepartment = string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase)
                    ? "Mantenimiento"
                    : null;
                if (string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase))
                {
                    await LoadTicketSparePartDispatchesAsync(ticket);
                    await PopulateMaintenanceFormDataAsync(ticket, normalizedSparePartsJson);
                }
                else
                {
                    await PopulateCreateFormDataAsync(ticket);
                }
                return View(ticket);
            }

            var dbTicket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (dbTicket == null)
            {
                return NotFound();
            }

            var originalTicket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (originalTicket == null)
            {
                return NotFound();
            }

            var canEditSolutionForm = CanEditSolutionForm();
            var canSetPriority = CanSetPriority();
            var canApproveCost = CanApproveMaintenanceCost();
            var isMaintenanceTicket = string.Equals(originalTicket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase);
            var sparePartInputs = new List<MaintenanceSparePartDispatchInput>();
            var selectedPartsById = new Dictionary<int, MaintenanceInventoryPart>();

            if (!canSetPriority)
            {
                ticket.Priority = originalTicket.Priority;
            }

            ticket.UnitCode = string.IsNullOrWhiteSpace(ticket.UnitCode)
                ? null
                : ticket.UnitCode.Trim();
            ticket.FailureCategory = string.IsNullOrWhiteSpace(ticket.FailureCategory)
                ? null
                : ticket.FailureCategory.Trim();
            ticket.DamagedElement = string.IsNullOrWhiteSpace(ticket.DamagedElement)
                ? null
                : ticket.DamagedElement.Trim();
            var selectedTechnicianCategories = ParseMultiSelectFieldValues(ticket.TechnicianCategory);
            ticket.TechnicianCategory = JoinMultiSelectFieldValues(selectedTechnicianCategories);
            var selectedTechnicians = ParseMultiSelectFieldValues(ticket.AssignedTechnician);
            ticket.AssignedTechnician = JoinMultiSelectFieldValues(selectedTechnicians);
            if ((ticket.TechnicianCategory?.Length ?? 0) > 60)
            {
                ModelState.AddModelError(nameof(Ticket.TechnicianCategory), "Selecciona menos categorias: excede el limite permitido.");
            }
            if ((ticket.AssignedTechnician?.Length ?? 0) > 100)
            {
                ModelState.AddModelError(nameof(Ticket.AssignedTechnician), "Selecciona menos tecnicos: excede el limite permitido.");
            }
            ticket.MaintenanceStage = string.IsNullOrWhiteSpace(ticket.MaintenanceStage)
                ? null
                : ticket.MaintenanceStage.Trim();
            ticket.CostApprovalNotes = string.IsNullOrWhiteSpace(ticket.CostApprovalNotes)
                ? null
                : ticket.CostApprovalNotes.Trim();

            if (isMaintenanceTicket)
            {
                ticket.Department = "Mantenimiento";

                if (selectedTechnicianCategories.Count == 0)
                {
                    ModelState.AddModelError(nameof(Ticket.TechnicianCategory), "Debe seleccionar al menos una categoria tecnica.");
                }

                if (selectedTechnicians.Count == 0)
                {
                    ModelState.AddModelError(nameof(Ticket.AssignedTechnician), "Debe asignar al menos un tecnico responsable.");
                }
                else if (selectedTechnicianCategories.Count > 0)
                {
                    var originalAssignees = ParseMultiSelectFieldValues(originalTicket.AssignedTechnician);
                    var selectedTechnicianRows = await _context.MaintenanceTechnicians
                        .AsNoTracking()
                        .Where(x => x.IsActive && selectedTechnicians.Contains(x.FullName))
                        .ToListAsync();

                    var selectedTechniciansByName = selectedTechnicianRows.ToDictionary(
                        x => x.FullName,
                        x => x,
                        StringComparer.OrdinalIgnoreCase);
                    var activeOrdersByTech = await GetMaintenanceActiveOrdersByTechnicianAsync(originalTicket.Id);

                    foreach (var technicianName in selectedTechnicians)
                    {
                        if (!selectedTechniciansByName.TryGetValue(technicianName, out var selectedTechnician))
                        {
                            ModelState.AddModelError(
                                nameof(Ticket.AssignedTechnician),
                                $"El tecnico '{technicianName}' no existe o esta inactivo.");
                            continue;
                        }

                        if (!selectedTechnicianCategories.Any(x =>
                                x.Equals(selectedTechnician.Category, StringComparison.OrdinalIgnoreCase)))
                        {
                            ModelState.AddModelError(
                                nameof(Ticket.AssignedTechnician),
                                $"El tecnico '{technicianName}' no coincide con las categorias seleccionadas.");
                        }

                        var isSameAssignee = originalAssignees.Any(x =>
                            x.Equals(technicianName, StringComparison.OrdinalIgnoreCase));

                        if (!selectedTechnician.IsAvailable && !isSameAssignee)
                        {
                            ModelState.AddModelError(
                                nameof(Ticket.AssignedTechnician),
                                $"El tecnico '{technicianName}' no esta disponible.");
                        }

                        var activeOrders = activeOrdersByTech.TryGetValue(technicianName, out var count)
                            ? count
                            : 0;
                        if (activeOrders >= Math.Max(1, selectedTechnician.MaxActiveOrders))
                        {
                            ModelState.AddModelError(
                                nameof(Ticket.AssignedTechnician),
                                $"El tecnico '{technicianName}' ya alcanzo su capacidad maxima ({selectedTechnician.MaxActiveOrders} ordenes activas).");
                        }
                    }
                }

                sparePartInputs = ParseMaintenanceSparePartInputs(sparePartsJson);
                normalizedSparePartsJson = SerializeMaintenanceSparePartInputs(sparePartInputs);

                if (!MaintenanceSites.Contains(ticket.Site, StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(Ticket.Site), "Seleccione un sitio valido para mantenimiento.");
                }
                else
                {
                    ticket.Site = MaintenanceSites
                        .First(site => site.Equals(ticket.Site, StringComparison.OrdinalIgnoreCase));
                }

                if (string.IsNullOrWhiteSpace(ticket.Tenencia) ||
                    !MaintenanceTenencias.Contains(ticket.Tenencia, StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(Ticket.Tenencia), "Seleccione la tenencia (Propio o Agregado).");
                }
                else
                {
                    ticket.Tenencia = MaintenanceTenencias
                        .First(value => value.Equals(ticket.Tenencia, StringComparison.OrdinalIgnoreCase));
                }

                if (string.IsNullOrWhiteSpace(ticket.MaintenanceStage) ||
                    !MaintenanceStageOptions.Contains(ticket.MaintenanceStage, StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(Ticket.MaintenanceStage), "Seleccione una etapa valida del taller.");
                }
                else
                {
                    ticket.MaintenanceStage = MaintenanceStageOptions
                        .First(value => value.Equals(ticket.MaintenanceStage, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(ticket.FailureCategory))
                {
                    if (!MaintenanceFailureCategories.Contains(ticket.FailureCategory, StringComparer.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(nameof(Ticket.FailureCategory), "Seleccione una categoria de falla valida.");
                    }
                    else
                    {
                        ticket.FailureCategory = MaintenanceFailureCategories
                            .First(value => value.Equals(ticket.FailureCategory, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (ticket.ExitOdometerKm.HasValue &&
                    ticket.FailureOdometerKm.HasValue &&
                    ticket.ExitOdometerKm.Value < ticket.FailureOdometerKm.Value)
                {
                    ModelState.AddModelError(nameof(Ticket.ExitOdometerKm), "El KM de salida no puede ser menor al KM de falla.");
                }

                if (sparePartInputs.Count > 0)
                {
                    var requestedIds = sparePartInputs
                        .Select(x => x.PartId)
                        .Distinct()
                        .ToList();

                    selectedPartsById = await _context.MaintenanceInventoryParts
                        .Where(x => x.IsActive && requestedIds.Contains(x.Id))
                        .ToDictionaryAsync(x => x.Id);

                    foreach (var item in sparePartInputs)
                    {
                        if (!selectedPartsById.TryGetValue(item.PartId, out var part))
                        {
                            ModelState.AddModelError(SparePartsModelStateKey, "Hay repuestos seleccionados que ya no estan disponibles.");
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(ticket.Site) &&
                            !NormalizeMaintenanceSiteForMatching(part.Site).Equals(
                                NormalizeMaintenanceSiteForMatching(ticket.Site),
                                StringComparison.OrdinalIgnoreCase))
                        {
                            ModelState.AddModelError(
                                SparePartsModelStateKey,
                                $"El repuesto {part.PartCode} - {part.PartName} no pertenece al sitio seleccionado.");
                        }

                        if (!string.IsNullOrWhiteSpace(ticket.Tenencia) &&
                            !part.Tenencia.Equals(ticket.Tenencia, StringComparison.OrdinalIgnoreCase))
                        {
                            ModelState.AddModelError(
                                SparePartsModelStateKey,
                                $"El repuesto {part.PartCode} - {part.PartName} no coincide con la tenencia seleccionada.");
                        }

                        if (part.QuantityOnHand < item.Quantity)
                        {
                            ModelState.AddModelError(
                                SparePartsModelStateKey,
                                $"Stock insuficiente para {part.PartCode} - {part.PartName}. Disponible: {part.QuantityOnHand}.");
                        }
                    }
                }
            }

            if (isMaintenanceTicket)
            {
                var existingDispatchTotals = await _context.TicketSparePartDispatches
                    .AsNoTracking()
                    .Where(x => x.TicketId == originalTicket.Id)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        TotalCordoba = g.Sum(x => x.TotalCostCordoba ?? 0m),
                        TotalUsd = g.Sum(x => x.TotalCostUsd ?? 0m)
                    })
                    .FirstOrDefaultAsync();

                var newDispatchCordoba = sparePartInputs
                    .Where(x => selectedPartsById.ContainsKey(x.PartId) && selectedPartsById[x.PartId].UnitCostCordoba.HasValue)
                    .Sum(x => selectedPartsById[x.PartId].UnitCostCordoba!.Value * x.Quantity);
                var newDispatchUsd = sparePartInputs
                    .Where(x => selectedPartsById.ContainsKey(x.PartId) && selectedPartsById[x.PartId].UnitCostUsd.HasValue)
                    .Sum(x => selectedPartsById[x.PartId].UnitCostUsd!.Value * x.Quantity);

                var estimatedTotalCordoba = (existingDispatchTotals?.TotalCordoba ?? 0m) +
                                            newDispatchCordoba +
                                            (ticket.ChangedComponentCostCordoba ?? 0m) +
                                            (ticket.LaborCostCordoba ?? 0m) +
                                            (ticket.ExternalCostCordoba ?? 0m);
                var estimatedTotalUsd = (existingDispatchTotals?.TotalUsd ?? 0m) +
                                        newDispatchUsd +
                                        (ticket.ChangedComponentCostUsd ?? 0m) +
                                        (ticket.LaborCostUsd ?? 0m) +
                                        (ticket.ExternalCostUsd ?? 0m);

                var requiresCostApproval = estimatedTotalCordoba >= MaintenanceCostApprovalThresholdCordoba ||
                                           estimatedTotalUsd >= MaintenanceCostApprovalThresholdUsd;

                var costInputsChanged =
                    sparePartInputs.Count > 0 ||
                    (originalTicket.ChangedComponentCostCordoba ?? 0m) != (ticket.ChangedComponentCostCordoba ?? 0m) ||
                    (originalTicket.ChangedComponentCostUsd ?? 0m) != (ticket.ChangedComponentCostUsd ?? 0m) ||
                    (originalTicket.LaborCostCordoba ?? 0m) != (ticket.LaborCostCordoba ?? 0m) ||
                    (originalTicket.LaborCostUsd ?? 0m) != (ticket.LaborCostUsd ?? 0m) ||
                    (originalTicket.ExternalCostCordoba ?? 0m) != (ticket.ExternalCostCordoba ?? 0m) ||
                    (originalTicket.ExternalCostUsd ?? 0m) != (ticket.ExternalCostUsd ?? 0m);

                ticket.RequiresCostApproval = requiresCostApproval;

                if (!requiresCostApproval)
                {
                    ticket.CostApproved = true;
                    ticket.CostApprovedBy = GetChangedBy();
                    ticket.CostApprovedAtUtc = DateTime.UtcNow;
                }
                else if (!canApproveCost)
                {
                    ticket.CostApproved = originalTicket.CostApproved;
                    ticket.CostApprovedBy = originalTicket.CostApprovedBy;
                    ticket.CostApprovedAtUtc = originalTicket.CostApprovedAtUtc;
                    ticket.CostApprovalNotes = originalTicket.CostApprovalNotes;
                }
                else if (costInputsChanged && originalTicket.CostApproved)
                {
                    ticket.CostApproved = false;
                    ticket.CostApprovedBy = null;
                    ticket.CostApprovedAtUtc = null;
                }

                if (requiresCostApproval && canApproveCost)
                {
                    if (ticket.CostApproved)
                    {
                        if (!originalTicket.CostApproved || string.IsNullOrWhiteSpace(ticket.CostApprovedBy))
                        {
                            ticket.CostApprovedBy = GetChangedBy();
                            ticket.CostApprovedAtUtc = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        ticket.CostApprovedBy = null;
                        ticket.CostApprovedAtUtc = null;
                    }
                }

                if (ticket.Status == TicketStatus.Closed &&
                    !string.Equals(ticket.MaintenanceStage, "Lista para entrega", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ticket.MaintenanceStage, "Cerrada", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(Ticket.MaintenanceStage), "Para cerrar la orden, primero debe estar en etapa 'Lista para entrega' o 'Cerrada'.");
                }

                if (ticket.Status == TicketStatus.Closed && ticket.RequiresCostApproval && !ticket.CostApproved)
                {
                    ModelState.AddModelError(nameof(Ticket.CostApproved), "No se puede cerrar la orden mientras la aprobacion de costos este pendiente.");
                }
            }

            var hasRecordedCosts = HasAnyStructuredCost(ticket) || HasAnyStructuredCost(originalTicket);
            if (isMaintenanceTicket)
            {
                hasRecordedCosts = hasRecordedCosts ||
                                   sparePartInputs.Count > 0 ||
                                   await _context.TicketSparePartDispatches
                                       .AsNoTracking()
                                       .AnyAsync(x =>
                                           x.TicketId == originalTicket.Id &&
                                           ((x.TotalCostCordoba ?? 0m) > 0m || (x.TotalCostUsd ?? 0m) > 0m));
            }

            await ValidateTicketCostCenterTraceabilityAsync(ticket, hasRecordedCosts);

            NormalizeTechnicalFieldsForRole(ticket, canEditSolutionForm, originalTicket);
            NormalizeSparePartFields(ticket);
            NormalizeComponentChangeFields(ticket);
            ValidateSolutionBusinessRules(ticket, originalTicket.AfterEvidencePath, afterEvidenceFile);

            if (!ModelState.IsValid)
            {
                ViewBag.ReturnDepartment = isMaintenanceTicket
                    ? "Mantenimiento"
                    : null;
                if (isMaintenanceTicket)
                {
                    await LoadTicketSparePartDispatchesAsync(ticket);
                    await PopulateMaintenanceFormDataAsync(ticket, normalizedSparePartsJson);
                }
                else
                {
                    await PopulateCreateFormDataAsync(ticket);
                }
                return View(ticket);
            }

            _context.Entry(dbTicket).Property(t => t.RowVersion).OriginalValue = ticket.RowVersion;

            dbTicket.TicketNumber = ticket.TicketNumber;
            dbTicket.RequestingUser = ticket.RequestingUser;
            dbTicket.Site = ticket.Site;
            dbTicket.Tenencia = ticket.Tenencia;
            dbTicket.UnitCode = ticket.UnitCode;
            dbTicket.FailureCategory = ticket.FailureCategory;
            dbTicket.DamagedElement = ticket.DamagedElement;
            dbTicket.FailureOdometerKm = ticket.FailureOdometerKm;
            dbTicket.ExitOdometerKm = ticket.ExitOdometerKm;
            dbTicket.TechnicianCategory = ticket.TechnicianCategory;
            dbTicket.MaintenanceStage = ticket.MaintenanceStage;
            dbTicket.Department = ticket.Department;
            dbTicket.CostCenterCode = ticket.CostCenterCode;
            dbTicket.IncidentType = ticket.IncidentType;
            dbTicket.Priority = ticket.Priority;
            dbTicket.Status = ticket.Status;
            dbTicket.AssignedTechnician = ticket.AssignedTechnician;
            dbTicket.LaborCostCordoba = ticket.LaborCostCordoba;
            dbTicket.LaborCostUsd = ticket.LaborCostUsd;
            dbTicket.ExternalCostCordoba = ticket.ExternalCostCordoba;
            dbTicket.ExternalCostUsd = ticket.ExternalCostUsd;
            dbTicket.RequiresCostApproval = ticket.RequiresCostApproval;
            dbTicket.CostApproved = ticket.CostApproved;
            dbTicket.CostApprovedBy = ticket.CostApprovedBy;
            dbTicket.CostApprovedAtUtc = ticket.CostApprovedAtUtc;
            dbTicket.CostApprovalNotes = ticket.CostApprovalNotes;
            dbTicket.SLADeadline = originalTicket.SLADeadline;
            dbTicket.Description = ticket.Description;

            dbTicket.AttachmentPath = await SaveUploadedFileAsync(
                attachmentFile,
                string.IsNullOrWhiteSpace(ticket.AttachmentPath)
                    ? originalTicket.AttachmentPath
                    : ticket.AttachmentPath);

            if (canEditSolutionForm)
            {
                dbTicket.InitialConditionNotes = ticket.InitialConditionNotes;
                dbTicket.RepairActionsPerformed = ticket.RepairActionsPerformed;
                dbTicket.RootCause = ticket.RootCause;
                dbTicket.SparePartRequired = ticket.SparePartRequired;
                dbTicket.SparePartPurchased = ticket.SparePartPurchased;
                dbTicket.SparePartDetails = ticket.SparePartDetails;
                dbTicket.ComponentChanged = ticket.ComponentChanged;
                dbTicket.ChangedComponentName = ticket.ChangedComponentName;
                dbTicket.ChangedComponentCost = ticket.ChangedComponentCost;
                dbTicket.ChangedComponentCostCordoba = ticket.ChangedComponentCostCordoba;
                dbTicket.ChangedComponentCostUsd = ticket.ChangedComponentCostUsd;
                dbTicket.TechnicalTestsPerformed = ticket.TechnicalTestsPerformed;
                dbTicket.UserConformityConfirmed = ticket.UserConformityConfirmed;
                dbTicket.PreventiveRecommendations = ticket.PreventiveRecommendations;
                dbTicket.Observations = ticket.Observations;

                dbTicket.BeforeEvidencePath = await SaveUploadedFileAsync(
                    beforeEvidenceFile,
                    string.IsNullOrWhiteSpace(ticket.BeforeEvidencePath)
                        ? originalTicket.BeforeEvidencePath
                        : ticket.BeforeEvidencePath);

                dbTicket.AfterEvidencePath = await SaveUploadedFileAsync(
                    afterEvidenceFile,
                    string.IsNullOrWhiteSpace(ticket.AfterEvidencePath)
                        ? originalTicket.AfterEvidencePath
                        : ticket.AfterEvidencePath);

                dbTicket.TechnicalSheetPath = await SaveUploadedFileAsync(
                    technicalSheetFile,
                    string.IsNullOrWhiteSpace(ticket.TechnicalSheetPath)
                        ? originalTicket.TechnicalSheetPath
                        : ticket.TechnicalSheetPath,
                    new[] { ".jpg", ".jpeg", ".png", ".webp" });

                dbTicket.ExitOrderPath = await SaveUploadedFileAsync(
                    exitOrderFile,
                    string.IsNullOrWhiteSpace(ticket.ExitOrderPath)
                        ? originalTicket.ExitOrderPath
                        : ticket.ExitOrderPath,
                    new[] { ".jpg", ".jpeg", ".png", ".webp" });
            }
            else
            {
                dbTicket.InitialConditionNotes = originalTicket.InitialConditionNotes;
                dbTicket.RepairActionsPerformed = originalTicket.RepairActionsPerformed;
                dbTicket.RootCause = originalTicket.RootCause;
                dbTicket.SparePartRequired = originalTicket.SparePartRequired;
                dbTicket.SparePartPurchased = originalTicket.SparePartPurchased;
                dbTicket.SparePartDetails = originalTicket.SparePartDetails;
                dbTicket.ComponentChanged = originalTicket.ComponentChanged;
                dbTicket.ChangedComponentName = originalTicket.ChangedComponentName;
                dbTicket.ChangedComponentCost = originalTicket.ChangedComponentCost;
                dbTicket.ChangedComponentCostCordoba = originalTicket.ChangedComponentCostCordoba;
                dbTicket.ChangedComponentCostUsd = originalTicket.ChangedComponentCostUsd;
                dbTicket.TechnicalTestsPerformed = originalTicket.TechnicalTestsPerformed;
                dbTicket.UserConformityConfirmed = originalTicket.UserConformityConfirmed;
                dbTicket.PreventiveRecommendations = originalTicket.PreventiveRecommendations;
                dbTicket.Observations = originalTicket.Observations;
                dbTicket.BeforeEvidencePath = originalTicket.BeforeEvidencePath;
                dbTicket.AfterEvidencePath = originalTicket.AfterEvidencePath;
                dbTicket.TechnicalSheetPath = originalTicket.TechnicalSheetPath;
                dbTicket.ExitOrderPath = originalTicket.ExitOrderPath;
            }

            if (isMaintenanceTicket && sparePartInputs.Count > 0)
            {
                var nowUtc = DateTime.UtcNow;
                var dispatchRows = new List<TicketSparePartDispatch>();
                var stockChanged = false;

                foreach (var item in sparePartInputs)
                {
                    if (!selectedPartsById.TryGetValue(item.PartId, out var part) || !part.IsActive)
                    {
                        ModelState.AddModelError(SparePartsModelStateKey, "Hay repuestos seleccionados que ya no estan disponibles.");
                        stockChanged = true;
                        break;
                    }

                    if (part.QuantityOnHand < item.Quantity)
                    {
                        ModelState.AddModelError(
                            SparePartsModelStateKey,
                            $"Stock insuficiente para {part.PartCode} - {part.PartName}. Disponible: {part.QuantityOnHand}.");
                        stockChanged = true;
                        break;
                    }

                    var totalCordoba = part.UnitCostCordoba.HasValue
                        ? part.UnitCostCordoba.Value * item.Quantity
                        : (decimal?)null;

                    var totalUsd = part.UnitCostUsd.HasValue
                        ? part.UnitCostUsd.Value * item.Quantity
                        : (decimal?)null;

                    part.QuantityOnHand -= item.Quantity;
                    part.QuantityIssued += item.Quantity;
                    part.LastIssueDate = nowUtc;
                    part.UpdatedAt = nowUtc;
                    ApplyMaintenanceInventoryStockStatusByQuantity(part);

                    dispatchRows.Add(new TicketSparePartDispatch
                    {
                        TicketId = dbTicket.Id,
                        MaintenanceInventoryPartId = part.Id,
                        PartCode = part.PartCode,
                        PartName = part.PartName,
                        UnitOfMeasure = part.UnitOfMeasure,
                        ItemCode = part.ItemCode,
                        PartNumber = part.ManufacturerPartNumber,
                        QuantityDispatched = item.Quantity,
                        UnitCostCordoba = part.UnitCostCordoba,
                        TotalCostCordoba = totalCordoba,
                        UnitCostUsd = part.UnitCostUsd,
                        TotalCostUsd = totalUsd,
                        CreatedAt = nowUtc
                    });
                }

                if (stockChanged)
                {
                    ViewBag.ReturnDepartment = "Mantenimiento";
                    await LoadTicketSparePartDispatchesAsync(ticket);
                    await PopulateMaintenanceFormDataAsync(ticket, normalizedSparePartsJson);
                    return View(ticket);
                }

                if (dispatchRows.Count > 0)
                {
                    _context.TicketSparePartDispatches.AddRange(dispatchRows);
                    dbTicket.SparePartRequired = true;

                    var extraDetails = string.Join(", ",
                        dispatchRows.Select(x => $"{x.PartCode} {x.PartName} x{x.QuantityDispatched}"));

                    if (!string.IsNullOrWhiteSpace(extraDetails))
                    {
                        var mergedDetails = string.IsNullOrWhiteSpace(dbTicket.SparePartDetails)
                            ? extraDetails
                            : $"{dbTicket.SparePartDetails}, {extraDetails}";

                        dbTicket.SparePartDetails = mergedDetails.Length > 500
                            ? mergedDetails.Substring(0, 500)
                            : mergedDetails;
                    }
                }
            }

            if (canEditSolutionForm &&
                dbTicket.Status == TicketStatus.Resolved &&
                IsTechnicalFormCompleted(dbTicket))
            {
                if (!(isMaintenanceTicket && dbTicket.RequiresCostApproval && !dbTicket.CostApproved))
                {
                    dbTicket.Status = TicketStatus.Closed;
                }
            }

            if (isMaintenanceTicket && dbTicket.Status == TicketStatus.Closed)
            {
                dbTicket.MaintenanceStage = "Cerrada";
            }

            if (CanModifyAttentionDate() && originalTicket.Priority != dbTicket.Priority)
            {
                dbTicket.SLADeadline = CalculateSLA(dbTicket.Priority);
            }

            if (dbTicket.Status == TicketStatus.Closed && originalTicket.Status != TicketStatus.Closed)
            {
                dbTicket.ClosedDate = DateTime.UtcNow;
            }

            if (dbTicket.Status != TicketStatus.Closed && originalTicket.Status == TicketStatus.Closed)
            {
                dbTicket.ClosedDate = null;
            }

            var ticketWasJustClosed =
                originalTicket.Status != TicketStatus.Closed &&
                dbTicket.Status == TicketStatus.Closed;

            try
            {
                await _context.SaveChangesAsync();
                await RegisterChangesAsync(originalTicket, dbTicket, GetChangedBy());

                if (isMaintenanceTicket &&
                    dbTicket.RequiresCostApproval &&
                    !dbTicket.CostApproved &&
                    !await HasMaintenanceAlertHistoryAsync(dbTicket.Id, MaintenanceAlertCostApprovalHistoryKey))
                {
                    await NotifyMaintenanceCostApprovalRequiredAsync(dbTicket);
                    await RegisterMaintenanceAlertHistoryAsync(
                        dbTicket.Id,
                        MaintenanceAlertCostApprovalHistoryKey,
                        "Pendiente",
                        "Alerta de aprobacion de costos enviada");
                }

                if (ticketWasJustClosed)
                {
                    await NotifyTicketClosedAsync(dbTicket);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "El registro fue modificado por otro usuario.");
                ticket.RowVersion = dbTicket.RowVersion;
                ViewBag.ReturnDepartment = isMaintenanceTicket
                    ? "Mantenimiento"
                    : null;
                if (isMaintenanceTicket)
                {
                    await LoadTicketSparePartDispatchesAsync(ticket);
                    await PopulateMaintenanceFormDataAsync(ticket, normalizedSparePartsJson);
                }
                else
                {
                    await PopulateCreateFormDataAsync(ticket);
                }
                return View(ticket);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No fue posible guardar los cambios del ticket.");
                ViewBag.ReturnDepartment = isMaintenanceTicket
                    ? "Mantenimiento"
                    : null;
                if (isMaintenanceTicket)
                {
                    await LoadTicketSparePartDispatchesAsync(ticket);
                    await PopulateMaintenanceFormDataAsync(ticket, normalizedSparePartsJson);
                }
                else
                {
                    await PopulateCreateFormDataAsync(ticket);
                }
                return View(ticket);
            }

            if (isMaintenanceTicket &&
                string.Equals(submitAction, "save_pdf", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(DownloadPdf), new
                {
                    id = dbTicket.Id,
                    department = "Mantenimiento"
                });
            }

            return RedirectToAction(nameof(Index), new
            {
                department = string.Equals(dbTicket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase)
                    ? "Mantenimiento"
                    : null
            });
        }

        private async Task LoadTicketSparePartDispatchesAsync(Ticket ticket)
        {
            ticket.SparePartDispatches = await _context.TicketSparePartDispatches
                .AsNoTracking()
                .Where(x => x.TicketId == ticket.Id)
                .OrderBy(x => x.PartName)
                .ThenBy(x => x.CreatedAt)
                .ToListAsync();
        }

        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> Delete(int? id, string? department = null)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            ViewBag.ReturnDepartment = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            return View(ticket);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,CoordinadorIT")]
        public async Task<IActionResult> DeleteConfirmed(int id, string? department = null)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            var returnDepartment = string.IsNullOrWhiteSpace(department)
                ? (string.Equals(ticket?.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase)
                    ? "Mantenimiento"
                    : null)
                : department.Trim();

            if (ticket != null)
            {
                _context.Tickets.Remove(ticket);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index), new { department = returnDepartment });
        }

        private async Task PopulateMaintenanceFormDataAsync(
            Ticket? ticket = null,
            string? selectedSparePartsJson = null,
            string? selectedMaintenanceUiMetaJson = null)
        {
            ticket ??= new Ticket
            {
                Department = "Mantenimiento",
                Status = TicketStatus.Open,
                Priority = PriorityLevel.Medium
            };

            ticket.Department = "Mantenimiento";
            if (string.IsNullOrWhiteSpace(ticket.MaintenanceStage))
            {
                ticket.MaintenanceStage = "Recibida";
            }

            await PopulateCreateFormDataAsync(ticket);

            var baseMaintenanceTypes = new List<string>
            {
                "Mantenimiento Correctivo",
                "Mantenimiento Preventivo",
                "Infraestructura",
                "Electricidad",
                "Mecanica",
                "Repuestos",
                "Inspeccion",
                "Otro Mantenimiento"
            };

            var dbMaintenanceTypes = await _context.Tickets
                .AsNoTracking()
                .Where(t =>
                    t.Department != null &&
                    t.Department == "Mantenimiento" &&
                    t.IncidentType != null &&
                    t.IncidentType != "")
                .Select(t => t.IncidentType!.Trim())
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            var maintenanceTypes = baseMaintenanceTypes
                .Concat(dbMaintenanceTypes.Where(t => !baseMaintenanceTypes.Contains(t, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(ticket.IncidentType) &&
                maintenanceTypes.All(t => !t.Equals(ticket.IncidentType, StringComparison.OrdinalIgnoreCase)))
            {
                maintenanceTypes.Insert(0, ticket.IncidentType);
            }

            ViewBag.IncidentTypeOptions = maintenanceTypes
                .Select(t => new SelectListItem(t, t))
                .ToList();

            ViewBag.SiteOptions = MaintenanceSites
                .Select(site => new SelectListItem(site, site))
                .ToList();

            ViewBag.TenenciaOptions = MaintenanceTenencias
                .Select(option => new SelectListItem(option, option))
                .ToList();

            ViewBag.MaintenanceFailureCategoryOptions = MaintenanceFailureCategories
                .Select(category => new SelectListItem(category, category))
                .ToList();

            ViewBag.MaintenanceStageOptions = MaintenanceStageOptions
                .Select(stage => new SelectListItem(stage, stage))
                .ToList();

            await PopulateMaintenanceTechnicianFormDataAsync(ticket);

            var maintenancePartsCatalog = await _context.MaintenanceInventoryParts
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.PartName)
                .Select(x => new MaintenanceSparePartCatalogItem
                {
                    Id = x.Id,
                    Label = $"{x.PartCode} - {x.PartName}",
                    Item = x.ItemCode,
                    PartNumber = x.ManufacturerPartNumber,
                    Site = x.Site,
                    Tenencia = x.Tenencia,
                    UnitOfMeasure = x.UnitOfMeasure,
                    Stock = x.QuantityOnHand,
                    UnitCostCordoba = x.UnitCostCordoba,
                    UnitCostUsd = x.UnitCostUsd
                })
                .ToListAsync();

            maintenancePartsCatalog = NormalizeLegacyCatalogIdentifiers(maintenancePartsCatalog);
            maintenancePartsCatalog = NormalizeMaintenanceCatalogSites(maintenancePartsCatalog);

            var maintenanceDuplicateCostByPart = BuildCatalogDuplicateCostFallback(maintenancePartsCatalog);
            maintenancePartsCatalog = maintenancePartsCatalog
                .Select(x =>
                {
                    maintenanceDuplicateCostByPart.TryGetValue(x.Id, out var duplicateFallback);
                    x.UnitCostCordoba = ResolveCatalogCost(x.UnitCostCordoba, duplicateFallback.Cordoba);
                    x.UnitCostUsd = ResolveCatalogCost(x.UnitCostUsd, duplicateFallback.Usd);
                    return x;
                })
                .ToList();

            ViewBag.MaintenanceSparePartsCatalogJson = JsonSerializer.Serialize(maintenancePartsCatalog, JsonOptions);
            ViewBag.SelectedSparePartsJson = string.IsNullOrWhiteSpace(selectedSparePartsJson)
                ? "[]"
                : selectedSparePartsJson;
            ViewBag.MaintenanceUiMetaJson = string.IsNullOrWhiteSpace(selectedMaintenanceUiMetaJson)
                ? "{}"
                : selectedMaintenanceUiMetaJson;

            ViewBag.MaintenanceMode = true;
        }

        private async Task PopulateMaintenanceTechnicianFormDataAsync(Ticket? ticket)
        {
            ticket ??= new Ticket();

            var catalog = await _context.MaintenanceTechnicians
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Category)
                .ThenBy(x => x.FullName)
                .Select(x => new
                {
                    x.FullName,
                    x.Category,
                    x.Shift,
                    x.MaxActiveOrders,
                    x.IsAvailable,
                    x.IsActive
                })
                .ToListAsync();

            var activeOrdersByTech = await GetMaintenanceActiveOrdersByTechnicianAsync(ticket?.Id);

            var catalogItems = catalog
                .Select(x =>
                {
                    var currentActiveOrders = activeOrdersByTech.TryGetValue(x.FullName, out var count) ? count : 0;
                    var maxActiveOrders = Math.Max(1, x.MaxActiveOrders);

                    return new MaintenanceTechnicianCatalogItem
                    {
                        FullName = x.FullName,
                        Category = x.Category,
                        Shift = x.Shift,
                        MaxActiveOrders = maxActiveOrders,
                        CurrentActiveOrders = currentActiveOrders,
                        IsOverCapacity = currentActiveOrders >= maxActiveOrders,
                        IsAvailable = x.IsAvailable,
                        IsActive = x.IsActive
                    };
                })
                .ToList();

            var selectedCategoryValues = ParseMultiSelectFieldValues(ticket?.TechnicianCategory);
            var categoryOptions = MaintenanceTechnicianCatalog.BuildCategoryOptions(
                catalogItems.Select(x => x.Category).Concat(selectedCategoryValues),
                selectedCategoryValues.FirstOrDefault());

            ViewBag.MaintenanceTechnicianCategoryOptions = categoryOptions
                .Select(x => new SelectListItem(x, x))
                .ToList();

            var technicianOptions = catalogItems
                .Where(x => x.IsAvailable && !x.IsOverCapacity)
                .OrderBy(x => x.Category)
                .ThenBy(x => x.FullName)
                .ToList();

            var selectedTechnicianValues = ParseMultiSelectFieldValues(ticket?.AssignedTechnician);
            foreach (var selectedTechnicianName in selectedTechnicianValues
                         .Where(selectedTechnicianName => technicianOptions.All(x =>
                             !x.FullName.Equals(selectedTechnicianName, StringComparison.OrdinalIgnoreCase))))
            {
                var existing = catalogItems
                    .FirstOrDefault(x => x.FullName.Equals(selectedTechnicianName, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    technicianOptions.Insert(0, existing);
                }
                else
                {
                    technicianOptions.Insert(0, new MaintenanceTechnicianCatalogItem
                    {
                        FullName = selectedTechnicianName,
                        Category = selectedCategoryValues.FirstOrDefault() ?? "No definida",
                        Shift = "N/A",
                        MaxActiveOrders = 1,
                        CurrentActiveOrders = 0,
                        IsOverCapacity = false,
                        IsAvailable = false,
                        IsActive = false
                    });
                }
            }

            ViewBag.TechnicianOptions = technicianOptions
                .Select(x =>
                {
                    var label = $"{x.FullName} ({x.CurrentActiveOrders}/{x.MaxActiveOrders})";
                    if (x.IsOverCapacity)
                    {
                        label += " - Capacidad completa";
                    }

                    if (!string.IsNullOrWhiteSpace(x.Shift))
                    {
                        label += $" - {x.Shift}";
                    }

                    return new SelectListItem(label, x.FullName);
                })
                .ToList();

            ViewBag.MaintenanceTechnicianCatalogJson = JsonSerializer.Serialize(catalogItems, JsonOptions);
        }

        private async Task<Dictionary<string, int>> GetMaintenanceActiveOrdersByTechnicianAsync(int? excludeTicketId = null)
        {
            var query = _context.Tickets
                .AsNoTracking()
                .Where(t =>
                    t.Department == "Mantenimiento" &&
                    t.Status != TicketStatus.Closed &&
                    !string.IsNullOrWhiteSpace(t.AssignedTechnician));

            if (excludeTicketId.HasValue)
            {
                query = query.Where(t => t.Id != excludeTicketId.Value);
            }

            var assignmentRows = await query
                .Select(t => t.AssignedTechnician!)
                .ToListAsync();

            var activeOrdersByTech = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var assignment in assignmentRows)
            {
                foreach (var technicianName in ParseMultiSelectFieldValues(assignment))
                {
                    activeOrdersByTech[technicianName] = activeOrdersByTech.TryGetValue(technicianName, out var current)
                        ? current + 1
                        : 1;
                }
            }

            return activeOrdersByTech;
        }

        private static List<string> ParseMultiSelectFieldValues(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return new List<string>();
            }

            return source
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string? JoinMultiSelectFieldValues(IEnumerable<string>? values)
        {
            var normalized = (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return normalized.Count == 0
                ? null
                : string.Join(", ", normalized);
        }

        private async Task PopulateCreateFormDataAsync(Ticket? ticket = null)
        {
            ViewBag.CanEditSolutionForm = CanEditSolutionForm();
            ViewBag.CanSetPriority = CanSetPriority();
            ViewBag.CanApproveCost = CanApproveMaintenanceCost();
            ViewBag.PreviewCreatedDate = DateTime.Now;
            ViewBag.PreviewTicketNumber = await GenerateTicketNumberAsync();
            ViewBag.SlaPreview = ToBusinessLocalTime(CalculateSLA(PriorityLevel.Medium)).ToString("dd/MM/yyyy HH:mm");

            var requestingUsers = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new { u.FullName, u.Email })
                .ToListAsync();

            var requestingUserOptions = requestingUsers
                .Select(u => new SelectListItem($"{u.FullName} ({u.Email})", u.FullName))
                .ToList();

            if (!string.IsNullOrWhiteSpace(ticket?.RequestingUser) &&
                requestingUserOptions.All(o => o.Value != ticket.RequestingUser))
            {
                requestingUserOptions.Insert(
                    0,
                    new SelectListItem($"{ticket.RequestingUser} (No activo)", ticket.RequestingUser));
            }

            var technicians = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && (
                    u.Role == UserRole.Technician ||
                    u.Role == UserRole.CoordinadorIT ||
                    u.Role == UserRole.Administrator))
                .Select(u => u.FullName)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var sites = new List<string>
            {
                "Oficina ASOMA",
                "Plantel Nagarote",
                "Plantel Las Lajitas",
                "Granada",
                "Diriamba",
                "Masaya",
                "Los Brasiles",
                "Casa Masaya",
                "Casa Miramar",
                "Casa de Alto Nagarote"
            };

            if (!string.IsNullOrWhiteSpace(ticket?.Site) &&
                !sites.Contains(ticket.Site, StringComparer.OrdinalIgnoreCase))
            {
                sites.Insert(0, ticket.Site);
            }

            var baseDepartments = new List<string>
            {
                "IT",
                "Operaciones",
                "Administracion",
                "Mantenimiento",
                "Finanzas"
            };

            var dbDepartments = await _context.Tickets
                .AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.Department))
                .Select(t => t.Department!.Trim())
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var departments = baseDepartments
                .Concat(dbDepartments.Where(d =>
                    !baseDepartments.Contains(d, StringComparer.OrdinalIgnoreCase) &&
                    !d.Equals("Compras", StringComparison.OrdinalIgnoreCase) &&
                    !d.Equals("Contabilidad", StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var baseIncidentTypes = new List<string>
            {
                "Hardware",
                "Software",
                "Red",
                "Servidores",
                "Mantenimiento Preventivo",
                "Mantenimiento Correctivo",
                "Infraestructura",
                "Electricidad",
                "Mecanica",
                "Repuestos",
                "Otros"
            };

            var dbIncidentTypes = await _context.Tickets
                .AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.IncidentType))
                .Select(t => t.IncidentType!.Trim())
                .Distinct()
                .OrderBy(i => i)
                .ToListAsync();

            var incidentTypes = baseIncidentTypes
                .Concat(dbIncidentTypes.Where(i => !baseIncidentTypes.Contains(i, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(ticket?.Department) &&
                departments.All(d => !d.Equals(ticket.Department, StringComparison.OrdinalIgnoreCase)))
            {
                departments.Insert(0, ticket.Department);
            }

            if (!string.IsNullOrWhiteSpace(ticket?.IncidentType) &&
                incidentTypes.All(i => !i.Equals(ticket.IncidentType, StringComparison.OrdinalIgnoreCase)))
            {
                incidentTypes.Insert(0, ticket.IncidentType);
            }

            ViewBag.RequestingUserOptions = requestingUserOptions;

            ViewBag.SiteOptions = sites
                .Select(s => new SelectListItem(s, s))
                .ToList();

            ViewBag.DepartmentOptions = departments
                .Select(d => new SelectListItem(d, d))
                .ToList();

            ViewBag.IncidentTypeOptions = incidentTypes
                .Select(i => new SelectListItem(i, i))
                .ToList();

            ViewBag.TechnicianOptions = technicians
                .Select(t => new SelectListItem(t, t))
                .ToList();

            var selectedCostCenter = NormalizeTicketCostCenterCode(ticket?.CostCenterCode);
            var activeCostCenters = await GetActiveFinanceCostCentersAsync();
            var ticketCostCenterOptions = activeCostCenters
                .OrderBy(x => x.Area)
                .ThenBy(x => x.CostCenterNumber)
                .Select(x => new SelectListItem(
                    $"#{x.CostCenterNumber} - {x.Area} - {x.CostCenterName}",
                    x.CostCenterNumber,
                    !string.IsNullOrWhiteSpace(selectedCostCenter) &&
                    x.CostCenterNumber.Equals(selectedCostCenter, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (!string.IsNullOrWhiteSpace(selectedCostCenter) &&
                ticketCostCenterOptions.All(x =>
                    !x.Value.Equals(selectedCostCenter, StringComparison.OrdinalIgnoreCase)))
            {
                ticketCostCenterOptions.Insert(
                    0,
                    new SelectListItem($"#{selectedCostCenter} - No activo", selectedCostCenter, true));
            }

            ViewBag.TicketCostCenterOptions = ticketCostCenterOptions;
        }

        private async Task PopulateFinanceFormDataAsync(FinanceRequestCreateViewModel model)
        {
            var isOcRequest = IsPurchaseOrderRequestType(model.RequestType);
            var ticketSeed = new Ticket
            {
                RequestingUser = model.RequestingUser ?? string.Empty,
                Site = model.Site ?? string.Empty,
                Department = isOcRequest ? "Inventario" : "Finanzas",
                IncidentType = model.RequestType ?? string.Empty,
                Priority = model.Priority
            };

            await PopulateCreateFormDataAsync(ticketSeed);

            if (isOcRequest)
            {
                if (!string.IsNullOrWhiteSpace(model.Site) &&
                    !PurchaseRequestSites.Contains(model.Site, StringComparer.OrdinalIgnoreCase))
                {
                    model.Site = string.Empty;
                }

                ViewBag.SiteOptions = PurchaseRequestSites
                    .Select(site => new SelectListItem(
                        site,
                        site,
                        string.Equals(model.Site, site, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(model.Site) &&
                    !FinanceRequestSites.Contains(model.Site, StringComparer.OrdinalIgnoreCase))
                {
                    model.Site = string.Empty;
                }

                ViewBag.SiteOptions = FinanceRequestSites
                    .Select(site => new SelectListItem(
                        site,
                        site,
                        string.Equals(model.Site, site, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var requestTypes = new[]
            {
                "Orden de compra",
                "Reembolso",
                "Pago a proveedor",
                "Anticipo de caja chica",
                "Transferencia interna",
                "Viaticos",
                "Otro"
            };

            ViewBag.FinanceRequestTypeOptions = requestTypes
                .Select(x => new SelectListItem(x, x, string.Equals(x, model.RequestType, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ViewBag.FinanceCurrencyOptions = new List<SelectListItem>
            {
                new("Cordobas (C$)", "C$", string.Equals(model.Currency, "C$", StringComparison.OrdinalIgnoreCase)),
                new("Dolares (USD)", "USD", string.Equals(model.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            };

            ViewBag.FinancePaymentMethodOptions = new List<SelectListItem>
            {
                new("Transferencia bancaria", "Transferencia", string.Equals(model.PaymentMethod, "Transferencia", StringComparison.OrdinalIgnoreCase)),
                new("Cheque", "Cheque", string.Equals(model.PaymentMethod, "Cheque", StringComparison.OrdinalIgnoreCase)),
                new("Efectivo", "Efectivo", string.Equals(model.PaymentMethod, "Efectivo", StringComparison.OrdinalIgnoreCase)),
                new("Tarjeta corporativa", "Tarjeta", string.Equals(model.PaymentMethod, "Tarjeta", StringComparison.OrdinalIgnoreCase))
            };

            var activeCostCenters = await GetActiveFinanceCostCentersAsync();
            if (activeCostCenters.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(model.CostCenter))
                {
                    model.CostCenter = activeCostCenters[0].CostCenterNumber;
                }

                ViewBag.FinanceCostCenterOptions = activeCostCenters
                    .OrderBy(x => x.Area)
                    .ThenBy(x => x.CostCenterNumber)
                    .Select(x => new SelectListItem(
                        $"#{x.CostCenterNumber} - {x.Area} - {x.CostCenterName}",
                        x.CostCenterNumber,
                        string.Equals(model.CostCenter, x.CostCenterNumber, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
            else
            {
                ViewBag.FinanceCostCenterOptions = new List<SelectListItem>();
            }

            ViewBag.CanSetPriority = CanSetPriority();
        }

        private static string BuildFinanceDescription(FinanceRequestCreateViewModel model, int requiredApprovals, FinanceBudgetSnapshot budget)
        {
            var centerDisplay = budget.CostCenterNumber;
            if (!string.IsNullOrWhiteSpace(budget.CostCenterName) &&
                !budget.CostCenterName.Equals(budget.CostCenterNumber, StringComparison.OrdinalIgnoreCase))
            {
                centerDisplay = $"{budget.CostCenterNumber} - {budget.CostCenterName}";
            }

            var summary = new StringBuilder();
            var headerLabel = IsPurchaseOrderRequestType(model.RequestType)
                ? "Solicitud de orden de compra"
                : "Solicitud financiera";
            summary.AppendLine($"{headerLabel}: {model.RequestType.Trim()}");
            summary.AppendLine($"Monto solicitado: {model.Currency.Trim()} {model.Amount:0.00}");
            summary.AppendLine($"Centro de costo: {centerDisplay}");
            summary.AppendLine($"Area: {SafeFinanceField(budget.Area)}");
            summary.AppendLine($"Fecha requerida: {model.NeededByDate:yyyy-MM-dd}");
            summary.AppendLine($"Proveedor o beneficiario: {SafeFinanceField(model.VendorName)}");
            summary.AppendLine($"Referencia: {SafeFinanceField(model.ReferenceNumber)}");
            summary.AppendLine($"Documento beneficiario: {SafeFinanceField(model.BeneficiaryDocument)}");
            summary.AppendLine($"Metodo de pago: {SafeFinanceField(model.PaymentMethod)}");
            summary.AppendLine($"Cuenta bancaria: {SafeFinanceField(model.BankAccount)}");
            summary.AppendLine($"Uso OC: {SafeFinanceField(model.OcUsage)}");
            summary.AppendLine($"Justificacion requerimiento OC: {SafeFinanceField(model.OcRequirementJustification)}");
            summary.AppendLine($"Justificacion cantidades OC: {SafeFinanceField(model.OcQuantityJustification)}");
            summary.AppendLine($"Estado OC: {SafeFinanceField(model.OcStatus)}");
            summary.AppendLine($"Cantidad de lineas OC: {model.OcLineCount}");
            summary.AppendLine($"Descripcion articulos OC: {SafeFinanceField(model.OcItemDescription)}");
            summary.AppendLine($"Orden de compra OC: {SafeFinanceField(model.OcPurchaseOrderNumber)}");
            summary.AppendLine($"Viatico desde: {(model.TravelFromDate.HasValue ? model.TravelFromDate.Value.ToString("yyyy-MM-dd") : "No aplica")}");
            summary.AppendLine($"Viatico hasta: {(model.TravelToDate.HasValue ? model.TravelToDate.Value.ToString("yyyy-MM-dd") : "No aplica")}");
            summary.AppendLine($"Aprobaciones requeridas: {requiredApprovals}");
            summary.AppendLine($"Periodo presupuestario: {budget.Year:D4}-{budget.Month:D2}");
            summary.AppendLine($"Presupuesto centro costo (C$): {budget.LimitCordoba:0.00}");
            summary.AppendLine($"Comprometido previo (C$): {budget.CommittedCordoba:0.00}");
            summary.AppendLine($"Disponible proyectado (C$): {budget.RemainingCordoba:0.00}");
            summary.AppendLine($"Uso proyectado (%): {budget.UsagePercentAfterRequest:0.##}");
            summary.AppendLine($"Umbral alerta (%): {budget.AlertThresholdPercent}");
            summary.AppendLine($"Riesgo presupuestario: {(budget.ExceedsAfterRequest ? "Sobregiro" : "Dentro de presupuesto")}");
            summary.AppendLine($"Bloqueo por sobregiro: {(budget.HardStopOnOverrun ? "Si" : "No")}");
            summary.AppendLine($"Requiere autorizacion por sobregiro: {(budget.RequireAuthorizationOnOverrun ? "Si" : "No")}");
            summary.AppendLine();
            summary.AppendLine("Justificacion:");
            summary.AppendLine(model.BusinessJustification.Trim());

            if (!string.IsNullOrWhiteSpace(model.AdditionalNotes))
            {
                summary.AppendLine();
                summary.AppendLine("Notas adicionales:");
                summary.AppendLine(model.AdditionalNotes.Trim());
            }

            return TrimToMax(summary.ToString(), 2000);
        }

        private static string? BuildFinanceObservations(FinanceRequestCreateViewModel model, FinanceBudgetSnapshot budget)
        {
            var originLabel = IsPurchaseOrderRequestType(model.RequestType)
                ? "Solicitud creada desde formulario de OC."
                : "Solicitud creada desde formulario financiero.";
            var budgetNote = budget.ExceedsAfterRequest
                ? budget.RequireAuthorizationOnOverrun
                    ? "Riesgo de sobregiro presupuestario. Requiere autorizacion adicional."
                    : "Riesgo de sobregiro presupuestario."
                : budget.IsNearThresholdAfterRequest
                    ? "Cerca del umbral presupuestario."
                    : "Dentro de presupuesto.";

            if (string.IsNullOrWhiteSpace(model.AdditionalNotes))
            {
                return $"{originLabel} {budgetNote}";
            }

            var notes = $"{originLabel} {budgetNote} Nota: {model.AdditionalNotes.Trim()}";
            return TrimToMax(notes, 2000);
        }

        private static bool IsPurchaseOrderRequestType(string? requestType)
        {
            if (string.IsNullOrWhiteSpace(requestType))
            {
                return false;
            }

            return requestType.Trim().Equals("Orden de compra", StringComparison.OrdinalIgnoreCase) ||
                   requestType.Trim().Equals("Solicitud de OC", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeOcWorkflowStage(string? ocStage)
        {
            return string.Equals(ocStage?.Trim(), "order", StringComparison.OrdinalIgnoreCase)
                ? "order"
                : "request";
        }

        private void ValidateFinanceRequestByType(FinanceRequestCreateViewModel model, IFormFile? supportDocumentFile)
        {
            var requestType = model.RequestType?.Trim() ?? string.Empty;
            var paymentMethod = model.PaymentMethod?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(requestType))
            {
                ModelState.AddModelError(nameof(model.RequestType), "Debes seleccionar un tipo de solicitud.");
                return;
            }

            if (IsPurchaseOrderRequestType(requestType))
            {
                var isOcOrderStage = NormalizeOcWorkflowStage(model.OcWorkflowStage) == "order";

                if (string.IsNullOrWhiteSpace(model.OcUsage))
                {
                    ModelState.AddModelError(nameof(model.OcUsage), "Debes indicar el uso de la solicitud de OC.");
                }

                if (string.IsNullOrWhiteSpace(model.OcRequirementJustification))
                {
                    ModelState.AddModelError(nameof(model.OcRequirementJustification), "Debes justificar a que se debe el requerimiento.");
                }

                if (string.IsNullOrWhiteSpace(model.OcQuantityJustification))
                {
                    ModelState.AddModelError(nameof(model.OcQuantityJustification), "Debes justificar por que son necesarias las cantidades solicitadas.");
                }

                if (string.IsNullOrWhiteSpace(model.OcStatus))
                {
                    ModelState.AddModelError(nameof(model.OcStatus), "Debes seleccionar el estado de OC.");
                }

                if (model.OcLineCount <= 0)
                {
                    ModelState.AddModelError(nameof(model.OcLineCount), "Debes indicar al menos una linea para la solicitud de OC.");
                }

                if (string.IsNullOrWhiteSpace(model.OcItemDescription))
                {
                    ModelState.AddModelError(nameof(model.OcItemDescription), "Debes detallar la descripcion de articulos.");
                }

                if (isOcOrderStage && string.IsNullOrWhiteSpace(model.OcPurchaseOrderNumber))
                {
                    ModelState.AddModelError(nameof(model.OcPurchaseOrderNumber), "Debes indicar el número de orden de compra para continuar.");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(model.CostCenter))
            {
                ModelState.AddModelError(nameof(model.CostCenter), "Debes indicar un centro de costo.");
            }

            if (string.IsNullOrWhiteSpace(paymentMethod))
            {
                ModelState.AddModelError(nameof(model.PaymentMethod), "Debes seleccionar un metodo de pago.");
            }

            if (!string.IsNullOrWhiteSpace(paymentMethod) &&
                paymentMethod.Equals("Transferencia", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(model.BankAccount))
            {
                ModelState.AddModelError(nameof(model.BankAccount), "Debes indicar la cuenta bancaria para pagos por transferencia.");
            }

            if (requestType.Equals("Pago a proveedor", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(model.VendorName))
                {
                    ModelState.AddModelError(nameof(model.VendorName), "Proveedor o beneficiario es obligatorio para pago a proveedor.");
                }

                if (string.IsNullOrWhiteSpace(model.ReferenceNumber))
                {
                    ModelState.AddModelError(nameof(model.ReferenceNumber), "La referencia (factura/OC) es obligatoria para pago a proveedor.");
                }

                if (string.IsNullOrWhiteSpace(model.BeneficiaryDocument))
                {
                    ModelState.AddModelError(nameof(model.BeneficiaryDocument), "Debes registrar el RUC/Cedula del proveedor.");
                }
            }

            if (requestType.Equals("Reembolso", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(model.ReferenceNumber))
            {
                ModelState.AddModelError(nameof(model.ReferenceNumber), "El reembolso requiere referencia de factura o documento.");
            }

            if (requestType.Equals("Viaticos", StringComparison.OrdinalIgnoreCase))
            {
                if (!model.TravelFromDate.HasValue || !model.TravelToDate.HasValue)
                {
                    ModelState.AddModelError(nameof(model.TravelFromDate), "Para viaticos debes indicar rango de fechas.");
                }
                else if (model.TravelToDate.Value.Date < model.TravelFromDate.Value.Date)
                {
                    ModelState.AddModelError(nameof(model.TravelToDate), "La fecha final de viatico no puede ser menor que la fecha inicial.");
                }
            }

            if (supportDocumentFile == null || supportDocumentFile.Length <= 0)
            {
                ModelState.AddModelError("supportDocumentFile", "Adjunta documento de soporte para cumplir el checklist de cierre.");
            }
        }

        private static bool IsFinanceTypeChecklistComplete(FinanceTicketMeta metadata, Ticket ticket)
        {
            if (metadata.RequestType.Equals("Pago a proveedor", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(metadata.VendorName) &&
                       !string.IsNullOrWhiteSpace(metadata.BeneficiaryDocument) &&
                       !string.IsNullOrWhiteSpace(metadata.ReferenceNumber);
            }

            if (metadata.RequestType.Equals("Viaticos", StringComparison.OrdinalIgnoreCase))
            {
                return metadata.TravelFromDate.HasValue &&
                       metadata.TravelToDate.HasValue &&
                       metadata.TravelToDate.Value.Date >= metadata.TravelFromDate.Value.Date &&
                       !string.IsNullOrWhiteSpace(metadata.ReferenceNumber);
            }

            if (metadata.RequestType.Equals("Reembolso", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(metadata.ReferenceNumber);
            }

            if (IsPurchaseOrderRequestType(metadata.RequestType))
            {
                return !string.IsNullOrWhiteSpace(metadata.OcUsage) &&
                       !string.IsNullOrWhiteSpace(metadata.OcRequirementJustification) &&
                       !string.IsNullOrWhiteSpace(metadata.OcQuantityJustification) &&
                       metadata.OcLineCount > 0 &&
                       !string.IsNullOrWhiteSpace(metadata.OcItemDescription);
            }

            if (metadata.RequestType.Equals("Transferencia interna", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(metadata.BankAccount) &&
                       !string.IsNullOrWhiteSpace(metadata.PaymentMethod);
            }

            return !string.IsNullOrWhiteSpace(ticket.AttachmentPath) &&
                   !string.IsNullOrWhiteSpace(metadata.BusinessJustification);
        }

        private static bool IsFinanceTicket(Ticket ticket)
        {
            return string.Equals(ticket.Department, "Finanzas", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRequesterSameAsApprover(Ticket ticket, string changedBy)
        {
            if (string.IsNullOrWhiteSpace(ticket.RequestingUser) || string.IsNullOrWhiteSpace(changedBy))
            {
                return false;
            }

            return ticket.RequestingUser.Trim().Equals(changedBy.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private async Task<List<FinanceCostCenter>> GetActiveFinanceCostCentersAsync()
        {
            try
            {
                return await _context.FinanceCostCenters
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Area)
                    .ThenBy(x => x.CostCenterNumber)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo cargar el catalogo de centros de costo de Finanzas.");
                return new List<FinanceCostCenter>();
            }
        }

        private async Task<decimal> GetFinanceUsdToCordobaRateAsync()
        {
            try
            {
                var configured = await _context.FinanceSettings
                    .AsNoTracking()
                    .Select(x => (decimal?)x.UsdToCordobaRate)
                    .FirstOrDefaultAsync();

                return configured.GetValueOrDefault(FinanceUsdToCordobaRateFallback);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo cargar el tipo de cambio de Finanzas. Se usara el valor por defecto.");
                return FinanceUsdToCordobaRateFallback;
            }
        }

        private static FinanceCostCenter? FindFinanceCostCenter(IEnumerable<FinanceCostCenter> centers, string? input)
        {
            var raw = string.IsNullOrWhiteSpace(input) ? string.Empty : input.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return centers.FirstOrDefault(c =>
                c.CostCenterNumber.Equals(raw, StringComparison.OrdinalIgnoreCase) ||
                c.CostCenterName.Equals(raw, StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith($"{c.CostCenterNumber} -", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains($"#{c.CostCenterNumber}", StringComparison.OrdinalIgnoreCase));
        }

        private static string? NormalizeTicketCostCenterCode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static bool HasAnyStructuredCost(Ticket ticket)
        {
            return (ticket.ChangedComponentCostCordoba ?? 0m) > 0m ||
                   (ticket.ChangedComponentCostUsd ?? 0m) > 0m ||
                   (ticket.LaborCostCordoba ?? 0m) > 0m ||
                   (ticket.LaborCostUsd ?? 0m) > 0m ||
                   (ticket.ExternalCostCordoba ?? 0m) > 0m ||
                   (ticket.ExternalCostUsd ?? 0m) > 0m;
        }

        private async Task ValidateTicketCostCenterTraceabilityAsync(Ticket ticket, bool hasSpending)
        {
            ticket.CostCenterCode = NormalizeTicketCostCenterCode(ticket.CostCenterCode);

            // Maintenance is operating without a finalized #CC catalog for now.
            // Keep #CC optional and never block create/edit flows in this area.
            if (string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!hasSpending)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ticket.CostCenterCode))
            {
                ModelState.AddModelError(
                    nameof(Ticket.CostCenterCode),
                    "Debes seleccionar un #CC cuando el ticket registra costos o gastos.");
                return;
            }

            var activeCostCenters = await GetActiveFinanceCostCentersAsync();
            if (activeCostCenters.Count == 0)
            {
                return;
            }

            var selectedCostCenter = FindFinanceCostCenter(activeCostCenters, ticket.CostCenterCode);
            if (selectedCostCenter == null)
            {
                ModelState.AddModelError(
                    nameof(Ticket.CostCenterCode),
                    "El #CC seleccionado no existe o esta inactivo.");
                return;
            }

            ticket.CostCenterCode = selectedCostCenter.CostCenterNumber;

            var ticketArea = ticket.Department?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ticketArea) ||
                ticketArea.Equals("Finanzas", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var areaHasConfiguredCenters = activeCostCenters.Any(x =>
                x.Area.Equals(ticketArea, StringComparison.OrdinalIgnoreCase));
            if (!areaHasConfiguredCenters)
            {
                return;
            }

            if (!selectedCostCenter.Area.Equals(ticketArea, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(Ticket.CostCenterCode),
                    $"El #CC seleccionado pertenece al area {selectedCostCenter.Area}. Debes seleccionar uno del area {ticketArea}.");
            }
        }

        private static bool IsFinanceCostCenterMatch(string? rawCostCenter, FinanceCostCenter? configuredCenter, string normalizedCostCenter)
        {
            var raw = string.IsNullOrWhiteSpace(rawCostCenter) ? string.Empty : rawCostCenter.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            if (configuredCenter == null)
            {
                return raw.Equals(normalizedCostCenter, StringComparison.OrdinalIgnoreCase);
            }

            return raw.Equals(configuredCenter.CostCenterNumber, StringComparison.OrdinalIgnoreCase) ||
                   raw.Equals(configuredCenter.CostCenterName, StringComparison.OrdinalIgnoreCase) ||
                   raw.StartsWith($"{configuredCenter.CostCenterNumber} -", StringComparison.OrdinalIgnoreCase) ||
                   raw.Contains($"#{configuredCenter.CostCenterNumber}", StringComparison.OrdinalIgnoreCase) ||
                   raw.Equals(normalizedCostCenter, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<FinanceApprovalPolicy> GetFinanceApprovalPolicyAsync(
            string? requestType,
            string? costCenter,
            decimal amount,
            string? currency)
        {
            var fallbackApprovals = GetFinanceRequiredApprovalsFallback(amount, currency, costCenter);
            var fallback = new FinanceApprovalPolicy
            {
                RequiredApprovals = fallbackApprovals,
                EscalationHours = FinanceDefaultEscalationHours
            };

            var usdToCordobaRate = await GetFinanceUsdToCordobaRateAsync();
            var amountCordoba = ConvertToCordoba(amount, currency, usdToCordobaRate);
            var normalizedRequestType = string.IsNullOrWhiteSpace(requestType) ? null : requestType.Trim();
            var normalizedCostCenter = string.IsNullOrWhiteSpace(costCenter) ? null : costCenter.Trim();
            var candidateCostCenters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(normalizedCostCenter))
            {
                candidateCostCenters.Add(normalizedCostCenter);

                var activeCenters = await GetActiveFinanceCostCentersAsync();
                var configuredCenter = FindFinanceCostCenter(activeCenters, normalizedCostCenter);
                if (configuredCenter != null)
                {
                    candidateCostCenters.Add(configuredCenter.CostCenterNumber);
                    candidateCostCenters.Add(configuredCenter.CostCenterName);
                }
            }

            try
            {
                var matchingRules = await _context.FinanceApprovalRules
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.MinAmountCordoba <= amountCordoba &&
                        (!x.MaxAmountCordoba.HasValue || amountCordoba <= x.MaxAmountCordoba.Value) &&
                        (x.RequestType == null || x.RequestType == "" || x.RequestType == normalizedRequestType) &&
                        (x.CostCenter == null || x.CostCenter == "" || candidateCostCenters.Contains(x.CostCenter)))
                    .ToListAsync();

                if (matchingRules.Count == 0)
                {
                    return fallback;
                }

                var selectedRule = matchingRules
                    .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.RequestType))
                    .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.CostCenter))
                    .ThenByDescending(x => x.MinAmountCordoba)
                    .ThenBy(x => x.Id)
                    .First();

                return new FinanceApprovalPolicy
                {
                    RequiredApprovals = Math.Clamp(selectedRule.RequiredApprovals, 1, 5),
                    EscalationHours = Math.Clamp(selectedRule.EscalationHours, 1, 240),
                    RuleId = selectedRule.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo cargar reglas de aprobación financiera desde BD. Se aplicará fallback.");
                return fallback;
            }
        }

        private static int GetFinanceRequiredApprovalsFallback(decimal amount, string? currency, string? costCenter)
        {
            var amountCordoba = ConvertToCordoba(amount, currency);
            var required = amountCordoba switch
            {
                <= FinanceApprovalTier1Cordoba => 1,
                <= FinanceApprovalTier2Cordoba => 2,
                _ => 3
            };

            if (!string.IsNullOrWhiteSpace(costCenter))
            {
                var normalizedCostCenter = costCenter.Trim();
                if (FinanceHighControlCostCenters.Contains(normalizedCostCenter) && required < 3)
                {
                    required += 1;
                }
            }

            return Math.Clamp(required, 1, 3);
        }

        private static decimal ConvertToCordoba(decimal amount, string? currency, decimal? usdToCordobaRate = null)
        {
            if (string.Equals(currency?.Trim(), "USD", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currency?.Trim(), "$", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Round(amount * (usdToCordobaRate ?? FinanceUsdToCordobaRateFallback), 2, MidpointRounding.AwayFromZero);
            }

            return amount;
        }

        private async Task<HashSet<string>> GetFinanceApproversAsync(int ticketId)
        {
            var approvers = await _context.TicketHistories
                .AsNoTracking()
                .Where(h => h.TicketId == ticketId && h.FieldChanged == FinanceApprovalStepHistoryKey && !string.IsNullOrWhiteSpace(h.ChangedBy))
                .Select(h => h.ChangedBy!)
                .Distinct()
                .ToListAsync();

            return approvers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<int> GetFinanceApprovalCountAsync(int ticketId)
        {
            var approvers = await GetFinanceApproversAsync(ticketId);
            return approvers.Count;
        }

        private async Task<FinanceBudgetSnapshot> BuildFinanceBudgetSnapshotAsync(
            string? costCenter,
            decimal requestAmount,
            string? currency,
            DateTime? referenceDateUtc = null)
        {
            var normalizedCostCenter = string.IsNullOrWhiteSpace(costCenter) ? "General" : costCenter.Trim();
            var usdToCordobaRate = await GetFinanceUsdToCordobaRateAsync();
            var referenceDate = referenceDateUtc ?? DateTime.UtcNow;
            var year = referenceDate.Year;
            var month = referenceDate.Month;
            var limit = FinanceBudgetByCostCenterCordoba.TryGetValue(normalizedCostCenter, out var configuredLimit)
                ? configuredLimit
                : FinanceBudgetByCostCenterCordoba["General"];
            var hardStop = true;
            var requireAuthorizationOnOverrun = true;
            var alertThresholdPercent = 80;
            var resolvedName = normalizedCostCenter;
            var resolvedArea = string.Empty;
            FinanceCostCenter? configuredCenter = null;

            var activeCenters = await GetActiveFinanceCostCentersAsync();
            configuredCenter = FindFinanceCostCenter(activeCenters, normalizedCostCenter);
            if (configuredCenter != null)
            {
                normalizedCostCenter = configuredCenter.CostCenterNumber;
                resolvedName = configuredCenter.CostCenterName;
                resolvedArea = configuredCenter.Area;
                limit = configuredCenter.MonthlyBudgetCordoba;
                hardStop = configuredCenter.HardStopOnOverrun;
                requireAuthorizationOnOverrun = configuredCenter.RequireAuthorizationOnOverrun;
                alertThresholdPercent = Math.Clamp(configuredCenter.AlertThresholdPercent, 1, 100);
            }

            try
            {
                var dbBudget = await _context.FinanceMonthlyBudgets
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.Year == year &&
                        x.Month == month &&
                        (x.CostCenter == normalizedCostCenter ||
                         x.CostCenter == resolvedName ||
                         x.CostCenter == "General"))
                    .OrderByDescending(x => x.CostCenter == normalizedCostCenter)
                    .ThenByDescending(x => x.CostCenter == resolvedName)
                    .FirstOrDefaultAsync();

                if (dbBudget != null)
                {
                    limit = dbBudget.BudgetLimitCordoba;
                    hardStop = dbBudget.HardStopOnOverrun;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo cargar presupuesto mensual de Finanzas desde BD. Se aplicará presupuesto fallback.");
            }

            var financeRows = await _context.Tickets
                .AsNoTracking()
                .Where(t =>
                    t.Department == "Finanzas" &&
                    t.CreatedDate.Year == year &&
                    t.CreatedDate.Month == month)
                .Select(t => new
                {
                    t.Status,
                    t.CostApproved,
                    t.Description
                })
                .ToListAsync();

            var committed = 0m;
            foreach (var row in financeRows)
            {
                if (row.Status == TicketStatus.Closed && !row.CostApproved)
                {
                    continue;
                }

                var meta = ParseFinanceTicketMeta(row.Description);
                if (!IsFinanceCostCenterMatch(meta.CostCenter, configuredCenter, normalizedCostCenter))
                {
                    continue;
                }

                committed += ConvertToCordoba(meta.Amount, meta.Currency, usdToCordobaRate);
            }

            var requestedCordoba = ConvertToCordoba(requestAmount, currency, usdToCordobaRate);
            var remainingAfterRequest = limit - committed - requestedCordoba;
            var usageAfterRequest = limit > 0
                ? (double)((committed + requestedCordoba) / limit) * 100d
                : 0d;

            return new FinanceBudgetSnapshot
            {
                LimitCordoba = limit,
                CommittedCordoba = committed,
                RemainingCordoba = remainingAfterRequest,
                ExceedsAfterRequest = remainingAfterRequest < 0,
                HardStopOnOverrun = hardStop,
                RequireAuthorizationOnOverrun = requireAuthorizationOnOverrun,
                AlertThresholdPercent = alertThresholdPercent,
                CostCenterNumber = normalizedCostCenter,
                CostCenterName = resolvedName,
                Area = resolvedArea,
                RequestedCordoba = requestedCordoba,
                UsagePercentAfterRequest = Math.Round(usageAfterRequest, 2),
                IsNearThresholdAfterRequest = usageAfterRequest >= alertThresholdPercent && usageAfterRequest < 100,
                Year = year,
                Month = month
            };
        }

        private static FinanceTicketMeta ParseFinanceTicketMeta(Ticket ticket)
        {
            return ParseFinanceTicketMeta(ticket.Description);
        }

        private static FinanceTicketMeta ParseFinanceTicketMeta(string? description)
        {
            var meta = new FinanceTicketMeta();
            if (string.IsNullOrWhiteSpace(description))
            {
                return meta;
            }

            var lines = description
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(x => x.Trim())
                .ToList();

            meta.RequestType = ReadFinanceValue(lines, "Solicitud financiera:");
            if (string.IsNullOrWhiteSpace(meta.RequestType))
            {
                meta.RequestType = ReadFinanceValue(lines, "Tipo de solicitud:");
            }

            var amountLine = ReadFinanceValue(lines, "Monto solicitado:");
            if (!string.IsNullOrWhiteSpace(amountLine))
            {
                if (amountLine.StartsWith("USD", StringComparison.OrdinalIgnoreCase) || amountLine.StartsWith("$", StringComparison.OrdinalIgnoreCase))
                {
                    meta.Currency = "USD";
                }
                else
                {
                    meta.Currency = "C$";
                }

                meta.Amount = ParseFinanceAmount(amountLine);
            }

            meta.CostCenter = ReadFinanceValue(lines, "Centro de costo:");
            meta.CostCenter = string.IsNullOrWhiteSpace(meta.CostCenter) ? "General" : meta.CostCenter;

            var neededByRaw = ReadFinanceValue(lines, "Fecha requerida:");
            if (DateTime.TryParse(neededByRaw, out var neededBy))
            {
                meta.NeededByDate = neededBy.Date;
            }

            meta.VendorName = ReadFinanceValue(lines, "Proveedor o beneficiario:");
            meta.ReferenceNumber = ReadFinanceValue(lines, "Referencia:");
            meta.BeneficiaryDocument = ReadFinanceValue(lines, "Documento beneficiario:");
            meta.PaymentMethod = ReadFinanceValue(lines, "Metodo de pago:");
            meta.BankAccount = ReadFinanceValue(lines, "Cuenta bancaria:");
            meta.OcUsage = ReadFinanceValue(lines, "Uso OC:");
            meta.OcRequirementJustification = ReadFinanceValue(lines, "Justificacion requerimiento OC:");
            meta.OcQuantityJustification = ReadFinanceValue(lines, "Justificacion cantidades OC:");
            meta.OcStatus = ReadFinanceValue(lines, "Estado OC:");
            if (string.IsNullOrWhiteSpace(meta.OcStatus))
            {
                meta.OcStatus = "PENDIENTE";
            }

            var ocLinesRaw = ReadFinanceValue(lines, "Cantidad de lineas OC:");
            if (int.TryParse(ocLinesRaw, out var ocLineCount))
            {
                meta.OcLineCount = Math.Max(0, ocLineCount);
            }

            meta.OcItemDescription = ReadFinanceValue(lines, "Descripcion articulos OC:");
            meta.OcPurchaseOrderNumber = ReadFinanceValue(lines, "Orden de compra OC:");

            var travelFromRaw = ReadFinanceValue(lines, "Viatico desde:");
            if (DateTime.TryParse(travelFromRaw, out var travelFrom))
            {
                meta.TravelFromDate = travelFrom.Date;
            }

            var travelToRaw = ReadFinanceValue(lines, "Viatico hasta:");
            if (DateTime.TryParse(travelToRaw, out var travelTo))
            {
                meta.TravelToDate = travelTo.Date;
            }

            var approvalsRaw = ReadFinanceValue(lines, "Aprobaciones requeridas:");
            if (int.TryParse(approvalsRaw, out var approvalsRequired))
            {
                meta.RequiredApprovals = Math.Max(0, approvalsRequired);
            }

            var justificationIndex = lines.FindIndex(l => l.Equals("Justificacion:", StringComparison.OrdinalIgnoreCase));
            if (justificationIndex >= 0 && justificationIndex + 1 < lines.Count)
            {
                var builder = new StringBuilder();
                for (var i = justificationIndex + 1; i < lines.Count; i++)
                {
                    if (lines[i].Equals("Notas adicionales:", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append(' ');
                    }

                    builder.Append(lines[i]);
                }
                meta.BusinessJustification = builder.ToString().Trim();
            }

            var notesIndex = lines.FindIndex(l => l.Equals("Notas adicionales:", StringComparison.OrdinalIgnoreCase));
            if (notesIndex >= 0 && notesIndex + 1 < lines.Count)
            {
                meta.AdditionalNotes = string.Join(" ", lines.Skip(notesIndex + 1)).Trim();
            }

            return meta;
        }

        private static string ReadFinanceValue(List<string> lines, string label)
        {
            var row = lines.FirstOrDefault(l => l.StartsWith(label, StringComparison.OrdinalIgnoreCase));
            if (row == null)
            {
                return string.Empty;
            }

            var value = row.Substring(label.Length).Trim();
            return value.Equals("No aplica", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : value;
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

        private static string AppendFinanceNote(string? existingNotes, string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return existingNotes ?? string.Empty;
            }

            var cleanNote = note.Trim();
            if (string.IsNullOrWhiteSpace(existingNotes))
            {
                return cleanNote;
            }

            return TrimToMax($"{existingNotes.Trim()} | {cleanNote}", 800);
        }

        private async Task RegisterFinanceAlertHistoryAsync(int ticketId, string key, string? oldValue, string? newValue)
        {
            var history = CreateHistory(ticketId, key, oldValue, newValue, "Sistema");
            _context.TicketHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        private static string SafeFinanceField(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "No especificado" : value.Trim();
        }

        private static string TrimToMax(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength
                ? value
                : value[..maxLength];
        }

        private List<MaintenanceSparePartDispatchInput> ParseMaintenanceSparePartInputs(string? sparePartsJson)
        {
            if (string.IsNullOrWhiteSpace(sparePartsJson))
            {
                return new List<MaintenanceSparePartDispatchInput>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<MaintenanceSparePartDispatchInput>>(sparePartsJson, JsonOptions)
                    ?? new List<MaintenanceSparePartDispatchInput>();

                var normalized = parsed
                    .Where(x => x.PartId > 0 && x.Quantity > 0)
                    .GroupBy(x => x.PartId)
                    .Select(g => new MaintenanceSparePartDispatchInput
                    {
                        PartId = g.Key,
                        Quantity = g.Sum(x => x.Quantity)
                    })
                    .ToList();

                var invalidRows = parsed.Count(x => x.PartId <= 0 || x.Quantity <= 0);
                if (invalidRows > 0)
                {
                    ModelState.AddModelError(SparePartsModelStateKey, "Hay lineas de repuesto invalidas (articulo o cantidad).");
                }

                if (normalized.Any(x => x.Quantity > 1000000))
                {
                    ModelState.AddModelError(SparePartsModelStateKey, "La cantidad de un repuesto excede el limite permitido.");
                }

                return normalized;
            }
            catch (JsonException)
            {
                ModelState.AddModelError(SparePartsModelStateKey, "No se pudo leer el detalle de repuestos. Intente agregarlo nuevamente.");
                return new List<MaintenanceSparePartDispatchInput>();
            }
        }

        private static string SerializeMaintenanceSparePartInputs(List<MaintenanceSparePartDispatchInput> inputs)
        {
            if (inputs.Count == 0)
            {
                return "[]";
            }

            return JsonSerializer.Serialize(inputs, JsonOptions);
        }

        private MaintenanceOrderUiMetaInput ParseMaintenanceOrderUiMetaInput(string? maintenanceUiMetaJson)
        {
            var result = new MaintenanceOrderUiMetaInput();

            if (string.IsNullOrWhiteSpace(maintenanceUiMetaJson))
            {
                return result;
            }

            try
            {
                result = JsonSerializer.Deserialize<MaintenanceOrderUiMetaInput>(maintenanceUiMetaJson, JsonOptions)
                    ?? new MaintenanceOrderUiMetaInput();
            }
            catch (JsonException)
            {
                ModelState.AddModelError(
                    MaintenanceUiMetaModelStateKey,
                    "No se pudieron leer los datos de costos y checklist. Revise los valores del formulario.");
                return new MaintenanceOrderUiMetaInput();
            }

            result.TemplatePreset = string.IsNullOrWhiteSpace(result.TemplatePreset)
                ? null
                : result.TemplatePreset.Trim();

            result.LaborCostCordoba = NormalizeMaintenanceMoney(result.LaborCostCordoba, "mano de obra (C$)");
            result.LaborCostUsd = NormalizeMaintenanceMoney(result.LaborCostUsd, "mano de obra ($)");
            result.ExternalCostCordoba = NormalizeMaintenanceMoney(result.ExternalCostCordoba, "servicio externo (C$)");
            result.ExternalCostUsd = NormalizeMaintenanceMoney(result.ExternalCostUsd, "servicio externo ($)");

            return result;
        }

        private decimal? NormalizeMaintenanceMoney(decimal? value, string label)
        {
            if (!value.HasValue)
            {
                return null;
            }

            if (value.Value < 0)
            {
                ModelState.AddModelError(
                    MaintenanceUiMetaModelStateKey,
                    $"El valor de {label} no puede ser negativo.");
                return null;
            }

            if (value.Value > 9999999999999999.99m)
            {
                ModelState.AddModelError(
                    MaintenanceUiMetaModelStateKey,
                    $"El valor de {label} excede el limite permitido.");
                return null;
            }

            return decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero);
        }

        private static string SerializeMaintenanceOrderUiMetaInput(MaintenanceOrderUiMetaInput input)
        {
            return JsonSerializer.Serialize(new MaintenanceOrderUiMetaInput
            {
                TemplatePreset = input.TemplatePreset,
                LaborCostCordoba = input.LaborCostCordoba,
                LaborCostUsd = input.LaborCostUsd,
                ExternalCostCordoba = input.ExternalCostCordoba,
                ExternalCostUsd = input.ExternalCostUsd,
                ChecklistInspectionCompleted = input.ChecklistInspectionCompleted,
                ChecklistPartsValidated = input.ChecklistPartsValidated,
                ChecklistFinalTestCompleted = input.ChecklistFinalTestCompleted,
                ChecklistUserConformityCompleted = input.ChecklistUserConformityCompleted
            }, JsonOptions);
        }

        private static void ApplyMaintenanceInventoryStockStatusByQuantity(MaintenanceInventoryPart model)
        {
            if (model.QuantityOnHand <= 0)
            {
                model.StockStatus = "Agotado";
                return;
            }

            if (model.QuantityOnHand <= model.MinimumStock)
            {
                model.StockStatus = "Bajo stock";
                return;
            }

            model.StockStatus = "Disponible";
        }

        private async Task<string?> SaveUploadedFileAsync(IFormFile? file, string? currentPath, string[]? allowedExtensions = null)
        {
            if (file == null || file.Length == 0)
            {
                return currentPath;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            allowedExtensions ??= new[] { ".jpg", ".jpeg", ".png", ".pdf" };

            if (!allowedExtensions.Contains(extension))
            {
                return currentPath;
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                return currentPath;
            }

            var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "evidence");
            Directory.CreateDirectory(uploadsRoot);

            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadsRoot, uniqueFileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/evidence/{uniqueFileName}";
        }

        private async Task EnsureMaintenanceAlertsAsync(IReadOnlyList<Ticket> tickets, DateTime nowUtc)
        {
            if (tickets.Count == 0)
            {
                return;
            }

            var ticketIds = tickets.Select(t => t.Id).Distinct().ToList();
            var maintenanceAlertKeys = new[]
            {
                MaintenanceAlertSla24HistoryKey,
                MaintenanceAlertSla2HistoryKey,
                MaintenanceAlertOverdueHistoryKey,
                MaintenanceAlertCostApprovalHistoryKey
            };

            var existingAlerts = await _context.TicketHistories
                .AsNoTracking()
                .Where(x => ticketIds.Contains(x.TicketId) && maintenanceAlertKeys.Contains(x.FieldChanged))
                .Select(x => new
                {
                    x.TicketId,
                    x.FieldChanged
                })
                .ToListAsync();

            var sentByTicket = existingAlerts
                .GroupBy(x => x.TicketId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.FieldChanged).ToHashSet(StringComparer.OrdinalIgnoreCase));

            var newAlertRows = new List<TicketHistory>();
            foreach (var ticket in tickets)
            {
                if (!sentByTicket.TryGetValue(ticket.Id, out var sentKeys))
                {
                    sentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    sentByTicket[ticket.Id] = sentKeys;
                }

                var remainingHours = (ticket.SLADeadline - nowUtc).TotalHours;
                if (remainingHours <= 24 && remainingHours > 2 && !sentKeys.Contains(MaintenanceAlertSla24HistoryKey))
                {
                    await NotifyMaintenanceOrderReminderAsync(ticket, "24h");
                    newAlertRows.Add(CreateHistory(ticket.Id, MaintenanceAlertSla24HistoryKey, null, "Enviada", "Sistema"));
                    sentKeys.Add(MaintenanceAlertSla24HistoryKey);
                }

                if (remainingHours <= 2 && remainingHours >= 0 && !sentKeys.Contains(MaintenanceAlertSla2HistoryKey))
                {
                    await NotifyMaintenanceOrderReminderAsync(ticket, "2h");
                    newAlertRows.Add(CreateHistory(ticket.Id, MaintenanceAlertSla2HistoryKey, null, "Enviada", "Sistema"));
                    sentKeys.Add(MaintenanceAlertSla2HistoryKey);
                }

                if (remainingHours < 0 && !sentKeys.Contains(MaintenanceAlertOverdueHistoryKey))
                {
                    await NotifyMaintenanceOrderReminderAsync(ticket, "Vencida");
                    newAlertRows.Add(CreateHistory(ticket.Id, MaintenanceAlertOverdueHistoryKey, null, "Enviada", "Sistema"));
                    sentKeys.Add(MaintenanceAlertOverdueHistoryKey);
                }

                if (ticket.RequiresCostApproval &&
                    !ticket.CostApproved &&
                    !sentKeys.Contains(MaintenanceAlertCostApprovalHistoryKey))
                {
                    await NotifyMaintenanceCostApprovalRequiredAsync(ticket);
                    newAlertRows.Add(CreateHistory(ticket.Id, MaintenanceAlertCostApprovalHistoryKey, null, "Enviada", "Sistema"));
                    sentKeys.Add(MaintenanceAlertCostApprovalHistoryKey);
                }
            }

            if (newAlertRows.Count > 0)
            {
                _context.TicketHistories.AddRange(newAlertRows);
                await _context.SaveChangesAsync();
            }
        }

        private async Task EnsureFinanceAlertsAsync(IReadOnlyList<Ticket> tickets, DateTime nowUtc)
        {
            if (tickets.Count == 0)
            {
                return;
            }

            var ticketIds = tickets.Select(t => t.Id).Distinct().ToList();
            var trackedFinanceTickets = await _context.Tickets
                .Where(t => ticketIds.Contains(t.Id))
                .ToListAsync();
            var financeAlertKeys = new[]
            {
                FinanceAlertSla24HistoryKey,
                FinanceAlertOverdueHistoryKey,
                FinanceAlertPendingApprovalHistoryKey,
                FinanceAlertEscalationHistoryKey
            };

            var existingAlerts = await _context.TicketHistories
                .AsNoTracking()
                .Where(x => ticketIds.Contains(x.TicketId) && financeAlertKeys.Contains(x.FieldChanged))
                .Select(x => new
                {
                    x.TicketId,
                    x.FieldChanged
                })
                .ToListAsync();

            var sentByTicket = existingAlerts
                .GroupBy(x => x.TicketId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.FieldChanged).ToHashSet(StringComparer.OrdinalIgnoreCase));

            var newAlertRows = new List<TicketHistory>();
            var hasTrackedUpdates = false;
            foreach (var ticket in trackedFinanceTickets)
            {
                if (!sentByTicket.TryGetValue(ticket.Id, out var sentKeys))
                {
                    sentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    sentByTicket[ticket.Id] = sentKeys;
                }

                var remainingHours = (ticket.SLADeadline - nowUtc).TotalHours;
                if (remainingHours <= 24 && remainingHours >= 0 && !sentKeys.Contains(FinanceAlertSla24HistoryKey))
                {
                    newAlertRows.Add(CreateHistory(ticket.Id, FinanceAlertSla24HistoryKey, null, "Enviada", "Sistema"));
                    sentKeys.Add(FinanceAlertSla24HistoryKey);
                }

                if (remainingHours < 0 && !sentKeys.Contains(FinanceAlertOverdueHistoryKey))
                {
                    newAlertRows.Add(CreateHistory(ticket.Id, FinanceAlertOverdueHistoryKey, null, "Enviada", "Sistema"));
                    sentKeys.Add(FinanceAlertOverdueHistoryKey);
                }

                if (ticket.RequiresCostApproval &&
                    !ticket.CostApproved &&
                    !sentKeys.Contains(FinanceAlertPendingApprovalHistoryKey))
                {
                    newAlertRows.Add(CreateHistory(ticket.Id, FinanceAlertPendingApprovalHistoryKey, null, "Pendiente", "Sistema"));
                    sentKeys.Add(FinanceAlertPendingApprovalHistoryKey);
                }

                if (ticket.RequiresCostApproval &&
                    !ticket.CostApproved &&
                    ticket.Status != TicketStatus.Closed &&
                    !sentKeys.Contains(FinanceAlertEscalationHistoryKey))
                {
                    var metadata = ParseFinanceTicketMeta(ticket);
                    var policy = await GetFinanceApprovalPolicyAsync(
                        metadata.RequestType,
                        metadata.CostCenter,
                        metadata.Amount,
                        metadata.Currency);
                    var ageHours = Math.Max(0d, (nowUtc - ticket.CreatedDate).TotalHours);

                    if (ageHours >= policy.EscalationHours)
                    {
                        ticket.FinanceEscalatedAtUtc = nowUtc;
                        ticket.FinanceEscalatedTo = "GerenciaGeneral";
                        if (ticket.Priority != PriorityLevel.Critical)
                        {
                            ticket.Priority = PriorityLevel.Critical;
                        }

                        hasTrackedUpdates = true;
                        newAlertRows.Add(CreateHistory(
                            ticket.Id,
                            FinanceAlertEscalationHistoryKey,
                            null,
                            $"Escalada por espera de aprobación > {policy.EscalationHours}h",
                            "Sistema"));
                        sentKeys.Add(FinanceAlertEscalationHistoryKey);
                    }
                }
            }

            if (newAlertRows.Count > 0 || hasTrackedUpdates)
            {
                if (newAlertRows.Count > 0)
                {
                    _context.TicketHistories.AddRange(newAlertRows);
                }

                await _context.SaveChangesAsync();
            }
        }

        private async Task<bool> HasMaintenanceAlertHistoryAsync(int ticketId, string fieldKey)
        {
            return await _context.TicketHistories
                .AsNoTracking()
                .AnyAsync(x => x.TicketId == ticketId && x.FieldChanged == fieldKey);
        }

        private async Task RegisterMaintenanceAlertHistoryAsync(int ticketId, string fieldKey, string? newValue, string changedBy)
        {
            _context.TicketHistories.Add(CreateHistory(ticketId, fieldKey, null, newValue, changedBy));
            await _context.SaveChangesAsync();
        }

        private static void NormalizeSparePartFields(Ticket ticket)
        {
            if (!ticket.SparePartRequired)
            {
                ticket.SparePartPurchased = false;
                ticket.SparePartDetails = null;
            }
        }

        private static void NormalizeComponentChangeFields(Ticket ticket)
        {
            if (!ticket.ComponentChanged)
            {
                ticket.ChangedComponentName = null;
                ticket.ChangedComponentCost = null;
                ticket.ChangedComponentCostCordoba = null;
                ticket.ChangedComponentCostUsd = null;
                return;
            }

            if (!ticket.ChangedComponentCostUsd.HasValue && ticket.ChangedComponentCost.HasValue)
            {
                ticket.ChangedComponentCostUsd = ticket.ChangedComponentCost;
            }

            ticket.ChangedComponentCost = ticket.ChangedComponentCostUsd;
        }

        private static void NormalizeTechnicalFieldsForRole(
            Ticket ticket,
            bool canEditSolutionForm,
            Ticket? originalTicket = null)
        {
            if (canEditSolutionForm)
            {
                return;
            }

            if (originalTicket == null)
            {
                ticket.InitialConditionNotes = null;
                ticket.RepairActionsPerformed = null;
                ticket.RootCause = null;
                ticket.SparePartRequired = false;
                ticket.SparePartPurchased = false;
                ticket.SparePartDetails = null;
                ticket.ComponentChanged = false;
                ticket.ChangedComponentName = null;
                ticket.ChangedComponentCost = null;
                ticket.ChangedComponentCostCordoba = null;
                ticket.ChangedComponentCostUsd = null;
                ticket.TechnicalTestsPerformed = null;
                ticket.UserConformityConfirmed = false;
                ticket.PreventiveRecommendations = null;
                ticket.Observations = null;
                ticket.BeforeEvidencePath = null;
                ticket.AfterEvidencePath = null;
                ticket.TechnicalSheetPath = null;
                ticket.ExitOrderPath = null;
                return;
            }

            ticket.InitialConditionNotes = originalTicket.InitialConditionNotes;
            ticket.RepairActionsPerformed = originalTicket.RepairActionsPerformed;
            ticket.RootCause = originalTicket.RootCause;
            ticket.SparePartRequired = originalTicket.SparePartRequired;
            ticket.SparePartPurchased = originalTicket.SparePartPurchased;
            ticket.SparePartDetails = originalTicket.SparePartDetails;
            ticket.ComponentChanged = originalTicket.ComponentChanged;
            ticket.ChangedComponentName = originalTicket.ChangedComponentName;
            ticket.ChangedComponentCost = originalTicket.ChangedComponentCost;
            ticket.ChangedComponentCostCordoba = originalTicket.ChangedComponentCostCordoba;
            ticket.ChangedComponentCostUsd = originalTicket.ChangedComponentCostUsd;
            ticket.TechnicalTestsPerformed = originalTicket.TechnicalTestsPerformed;
            ticket.UserConformityConfirmed = originalTicket.UserConformityConfirmed;
            ticket.PreventiveRecommendations = originalTicket.PreventiveRecommendations;
            ticket.Observations = originalTicket.Observations;
            ticket.BeforeEvidencePath = originalTicket.BeforeEvidencePath;
            ticket.AfterEvidencePath = originalTicket.AfterEvidencePath;
            ticket.TechnicalSheetPath = originalTicket.TechnicalSheetPath;
            ticket.ExitOrderPath = originalTicket.ExitOrderPath;
        }

        private void ValidateSolutionBusinessRules(
            Ticket ticket,
            string? currentAfterEvidencePath,
            IFormFile? afterEvidenceFile)
        {
            var requiresFinalEvidence =
                ticket.Status == TicketStatus.Resolved ||
                ticket.Status == TicketStatus.Closed;

            var hasFinalEvidence =
                !string.IsNullOrWhiteSpace(ticket.AfterEvidencePath) ||
                !string.IsNullOrWhiteSpace(currentAfterEvidencePath) ||
                (afterEvidenceFile != null && afterEvidenceFile.Length > 0);

            if (requiresFinalEvidence && !hasFinalEvidence)
            {
                ModelState.AddModelError(
                    nameof(Ticket.AfterEvidencePath),
                    "Debe adjuntar evidencia final cuando la incidencia est? Resuelta o Cerrada.");
            }

            if (ticket.SparePartRequired && string.IsNullOrWhiteSpace(ticket.SparePartDetails))
            {
                ModelState.AddModelError(
                    nameof(Ticket.SparePartDetails),
                    "Debe especificar el detalle del repuesto cuando marca que fue requerido.");
            }

            if (ticket.ComponentChanged && string.IsNullOrWhiteSpace(ticket.ChangedComponentName))
            {
                ModelState.AddModelError(
                    nameof(Ticket.ChangedComponentName),
                    "Debe indicar cual componente fue cambiado.");
            }

            if (ticket.ComponentChanged && ticket.ChangedComponentCostCordoba == null)
            {
                ModelState.AddModelError(
                    nameof(Ticket.ChangedComponentCostCordoba),
                    "Debe indicar el costo del componente cambiado en C$.");
            }

            if (ticket.ComponentChanged && ticket.ChangedComponentCostUsd == null)
            {
                ModelState.AddModelError(
                    nameof(Ticket.ChangedComponentCostUsd),
                    "Debe indicar el costo del componente cambiado en $.");
            }

            if (string.Equals(ticket.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase))
            {
                if (ticket.Status == TicketStatus.Closed &&
                    ticket.RequiresCostApproval &&
                    !ticket.CostApproved)
                {
                    ModelState.AddModelError(
                        nameof(Ticket.CostApproved),
                        "No se puede cerrar una orden de mantenimiento con aprobacion de costos pendiente.");
                }
            }

        }

        private static bool IsTechnicalFormCompleted(Ticket ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket.InitialConditionNotes) ||
                string.IsNullOrWhiteSpace(ticket.RepairActionsPerformed) ||
                string.IsNullOrWhiteSpace(ticket.RootCause) ||
                string.IsNullOrWhiteSpace(ticket.TechnicalTestsPerformed) ||
                !ticket.UserConformityConfirmed ||
                string.IsNullOrWhiteSpace(ticket.AfterEvidencePath))
            {
                return false;
            }

            if (ticket.SparePartRequired && string.IsNullOrWhiteSpace(ticket.SparePartDetails))
            {
                return false;
            }

            if (ticket.ComponentChanged &&
                (string.IsNullOrWhiteSpace(ticket.ChangedComponentName) ||
                 !ticket.ChangedComponentCostCordoba.HasValue ||
                 !ticket.ChangedComponentCostUsd.HasValue))
            {
                return false;
            }

            return true;
        }
        private bool CanSetPriority()
        {
            return User.IsInRole(nameof(UserRole.Technician)) ||
                   User.IsInRole(nameof(UserRole.Administrator));
        }
        private bool CanEditSolutionForm()
        {
            return User.IsInRole(nameof(UserRole.Technician)) ||
                   User.IsInRole(nameof(UserRole.Administrator));
        }

        private bool CanModifyAttentionDate()
        {
            return User.IsInRole(nameof(UserRole.Administrator)) ||
                   User.IsInRole(nameof(UserRole.CoordinadorIT));
        }

        private bool CanApproveMaintenanceCost()
        {
            return User.IsInRole(nameof(UserRole.Administrator)) ||
                   User.IsInRole(nameof(UserRole.CoordinadorIT)) ||
                   User.IsInRole(nameof(UserRole.GerenciaGeneral));
        }

        private async Task RegisterChangesAsync(Ticket original, Ticket updated, string changedBy)
        {
            var changes = new List<TicketHistory>();
            var isMaintenance = IsMaintenanceTicketForAudit(original, updated);

            AddHistoryChange(changes, updated.Id, "Status", original.Status.ToString(), updated.Status.ToString(), changedBy);
            AddHistoryChange(changes, updated.Id, "Priority", original.Priority.ToString(), updated.Priority.ToString(), changedBy);
            AddHistoryChange(changes, updated.Id, "AssignedTechnician", original.AssignedTechnician, updated.AssignedTechnician, changedBy);
            AddHistoryChange(changes, updated.Id, "TechnicianCategory", original.TechnicianCategory, updated.TechnicianCategory, changedBy);
            AddHistoryChange(changes, updated.Id, "CostCenterCode", original.CostCenterCode, updated.CostCenterCode, changedBy);
            AddHistoryChange(changes, updated.Id, "MaintenanceStage", original.MaintenanceStage, updated.MaintenanceStage, changedBy);
            AddHistoryChange(changes, updated.Id, "RequestingUser", original.RequestingUser, updated.RequestingUser, changedBy);
            AddHistoryChange(changes, updated.Id, "Site", original.Site, updated.Site, changedBy);
            AddHistoryChange(changes, updated.Id, "Tenencia", original.Tenencia, updated.Tenencia, changedBy);
            AddHistoryChange(changes, updated.Id, "Department", original.Department, updated.Department, changedBy);
            AddHistoryChange(changes, updated.Id, "IncidentType", original.IncidentType, updated.IncidentType, changedBy);
            AddHistoryChange(changes, updated.Id, "UnitCode", original.UnitCode, updated.UnitCode, changedBy);
            AddHistoryChange(changes, updated.Id, "Description", original.Description, updated.Description, changedBy);
            AddHistoryChange(changes, updated.Id, "CostApproved", FormatBoolHistory(original.CostApproved), FormatBoolHistory(updated.CostApproved), changedBy);
            AddHistoryChange(changes, updated.Id, "RequiresCostApproval", FormatBoolHistory(original.RequiresCostApproval), FormatBoolHistory(updated.RequiresCostApproval), changedBy);
            AddHistoryChange(changes, updated.Id, "CostApprovedBy", original.CostApprovedBy, updated.CostApprovedBy, changedBy);
            AddHistoryChange(changes, updated.Id, "CostApprovedAtUtc", FormatDateTimeHistory(original.CostApprovedAtUtc), FormatDateTimeHistory(updated.CostApprovedAtUtc), changedBy);
            AddHistoryChange(changes, updated.Id, "CostApprovalNotes", original.CostApprovalNotes, updated.CostApprovalNotes, changedBy);
            AddHistoryChange(changes, updated.Id, "LaborCostCordoba", FormatDecimalHistory(original.LaborCostCordoba), FormatDecimalHistory(updated.LaborCostCordoba), changedBy);
            AddHistoryChange(changes, updated.Id, "LaborCostUsd", FormatDecimalHistory(original.LaborCostUsd), FormatDecimalHistory(updated.LaborCostUsd), changedBy);
            AddHistoryChange(changes, updated.Id, "ExternalCostCordoba", FormatDecimalHistory(original.ExternalCostCordoba), FormatDecimalHistory(updated.ExternalCostCordoba), changedBy);
            AddHistoryChange(changes, updated.Id, "ExternalCostUsd", FormatDecimalHistory(original.ExternalCostUsd), FormatDecimalHistory(updated.ExternalCostUsd), changedBy);
            AddHistoryChange(changes, updated.Id, "SLADeadline", FormatDateTimeHistory(original.SLADeadline), FormatDateTimeHistory(updated.SLADeadline), changedBy);
            AddHistoryChange(changes, updated.Id, "ClosedDate", FormatDateTimeHistory(original.ClosedDate), FormatDateTimeHistory(updated.ClosedDate), changedBy);
            AddHistoryChange(changes, updated.Id, "AttachmentPath", original.AttachmentPath, updated.AttachmentPath, changedBy);

            if (isMaintenance)
            {
                AddHistoryChange(changes, updated.Id, "FailureCategory", original.FailureCategory, updated.FailureCategory, changedBy);
                AddHistoryChange(changes, updated.Id, "DamagedElement", original.DamagedElement, updated.DamagedElement, changedBy);
                AddHistoryChange(changes, updated.Id, "FailureOdometerKm", FormatDecimalHistory(original.FailureOdometerKm), FormatDecimalHistory(updated.FailureOdometerKm), changedBy);
                AddHistoryChange(changes, updated.Id, "ExitOdometerKm", FormatDecimalHistory(original.ExitOdometerKm), FormatDecimalHistory(updated.ExitOdometerKm), changedBy);
                AddHistoryChange(changes, updated.Id, "InitialConditionNotes", original.InitialConditionNotes, updated.InitialConditionNotes, changedBy);
                AddHistoryChange(changes, updated.Id, "RepairActionsPerformed", original.RepairActionsPerformed, updated.RepairActionsPerformed, changedBy);
                AddHistoryChange(changes, updated.Id, "RootCause", original.RootCause, updated.RootCause, changedBy);
                AddHistoryChange(changes, updated.Id, "TechnicalTestsPerformed", original.TechnicalTestsPerformed, updated.TechnicalTestsPerformed, changedBy);
                AddHistoryChange(changes, updated.Id, "SparePartRequired", FormatBoolHistory(original.SparePartRequired), FormatBoolHistory(updated.SparePartRequired), changedBy);
                AddHistoryChange(changes, updated.Id, "SparePartPurchased", FormatBoolHistory(original.SparePartPurchased), FormatBoolHistory(updated.SparePartPurchased), changedBy);
                AddHistoryChange(changes, updated.Id, "SparePartDetails", original.SparePartDetails, updated.SparePartDetails, changedBy);
                AddHistoryChange(changes, updated.Id, "ComponentChanged", FormatBoolHistory(original.ComponentChanged), FormatBoolHistory(updated.ComponentChanged), changedBy);
                AddHistoryChange(changes, updated.Id, "ChangedComponentName", original.ChangedComponentName, updated.ChangedComponentName, changedBy);
                AddHistoryChange(changes, updated.Id, "ChangedComponentCostCordoba", FormatDecimalHistory(original.ChangedComponentCostCordoba), FormatDecimalHistory(updated.ChangedComponentCostCordoba), changedBy);
                AddHistoryChange(changes, updated.Id, "ChangedComponentCostUsd", FormatDecimalHistory(original.ChangedComponentCostUsd), FormatDecimalHistory(updated.ChangedComponentCostUsd), changedBy);
                AddHistoryChange(changes, updated.Id, "UserConformityConfirmed", FormatBoolHistory(original.UserConformityConfirmed), FormatBoolHistory(updated.UserConformityConfirmed), changedBy);
                AddHistoryChange(changes, updated.Id, "PreventiveRecommendations", original.PreventiveRecommendations, updated.PreventiveRecommendations, changedBy);
                AddHistoryChange(changes, updated.Id, "Observations", original.Observations, updated.Observations, changedBy);
                AddHistoryChange(changes, updated.Id, "BeforeEvidencePath", original.BeforeEvidencePath, updated.BeforeEvidencePath, changedBy);
                AddHistoryChange(changes, updated.Id, "AfterEvidencePath", original.AfterEvidencePath, updated.AfterEvidencePath, changedBy);
                AddHistoryChange(changes, updated.Id, "TechnicalSheetPath", original.TechnicalSheetPath, updated.TechnicalSheetPath, changedBy);
                AddHistoryChange(changes, updated.Id, "ExitOrderPath", original.ExitOrderPath, updated.ExitOrderPath, changedBy);
            }

            if (changes.Any())
            {
                _context.TicketHistories.AddRange(changes);
                await _context.SaveChangesAsync();
            }
        }

        private static bool IsMaintenanceTicketForAudit(Ticket original, Ticket updated)
        {
            return string.Equals(original.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(updated.Department, "Mantenimiento", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddHistoryChange(
            List<TicketHistory> target,
            int ticketId,
            string field,
            string? oldValue,
            string? newValue,
            string changedBy)
        {
            var normalizedOld = NormalizeHistoryValue(oldValue);
            var normalizedNew = NormalizeHistoryValue(newValue);
            if (string.Equals(normalizedOld ?? string.Empty, normalizedNew ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            target.Add(CreateHistory(ticketId, field, normalizedOld, normalizedNew, changedBy));
        }

        private static string? NormalizeHistoryValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();
            return normalized.Length > 500 ? normalized.Substring(0, 500) : normalized;
        }

        private static string FormatBoolHistory(bool value)
        {
            return value ? "Si" : "No";
        }

        private static string? FormatDecimalHistory(decimal? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.##", CultureInfo.InvariantCulture)
                : null;
        }

        private static string? FormatDateTimeHistory(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : null;
        }

        private static TicketHistory CreateHistory(
            int ticketId,
            string field,
            string? oldValue,
            string? newValue,
            string changedBy)
        {
            return new TicketHistory
            {
                TicketId = ticketId,
                FieldChanged = field,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedBy = changedBy,
                ChangeDate = DateTime.UtcNow
            };
        }

        private async Task<string> GenerateTicketNumberAsync()
        {
            var year = DateTime.UtcNow.Year;

            var lastTicket = await _context.Tickets
                .Where(t => t.CreatedDate.Year == year)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync();

            var nextNumber = 1;

            if (lastTicket?.TicketNumber != null)
            {
                var parts = lastTicket.TicketNumber.Split('-');

                if (parts.Length == 3 && int.TryParse(parts[2], out var lastSequence))
                {
                    nextNumber = lastSequence + 1;
                }
            }

            return $"TK-{year}-{nextNumber:D3}";
        }

        private static readonly TimeZoneInfo SlaBusinessTimeZone = ResolveSlaBusinessTimeZone();

        private static DateTime CalculateSLA(PriorityLevel priority)
        {
            var slaHours = GetSlaHours(priority);
            var nowBusiness = ToBusinessLocalTime(DateTime.UtcNow);
            var start = MoveToNextWorkingMinute(nowBusiness);
            var deadlineBusiness = AddWorkingHours(start, slaHours);

            return TimeZoneInfo.ConvertTimeToUtc(deadlineBusiness, SlaBusinessTimeZone);
        }

        private static TimeZoneInfo ResolveSlaBusinessTimeZone()
        {
            try
            {
                // Nicaragua: UTC-06:00 sin DST.
                return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }

        private static DateTime ToBusinessLocalTime(DateTime utcDateTime)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, SlaBusinessTimeZone);
        }

        private static int GetSlaHours(PriorityLevel priority)
        {
            return priority switch
            {
                PriorityLevel.Low => 48,
                PriorityLevel.Medium => 24,
                PriorityLevel.High => 8,
                PriorityLevel.Critical => 4,
                _ => 24
            };
        }

        private static DateTime MoveToNextWorkingMinute(DateTime localDateTime)
        {
            var current = localDateTime;

            while (true)
            {
                if (current.DayOfWeek == DayOfWeek.Sunday)
                {
                    current = current.Date.AddDays(1).AddHours(8);
                    continue;
                }

                var start = current.Date.AddHours(8);
                var end = current.Date.AddHours(current.DayOfWeek == DayOfWeek.Saturday ? 13 : 17);

                if (current < start)
                {
                    return start;
                }

                if (current >= end)
                {
                    current = current.Date.AddDays(1).AddHours(8);
                    continue;
                }

                return current;
            }
        }

        private static DateTime AddWorkingHours(DateTime startLocal, int hours)
        {
            var remaining = (double)hours;
            var current = MoveToNextWorkingMinute(startLocal);

            while (remaining > 0)
            {
                current = MoveToNextWorkingMinute(current);

                var shiftEnd = current.Date.AddHours(current.DayOfWeek == DayOfWeek.Saturday ? 13 : 17);
                var available = (shiftEnd - current).TotalHours;

                if (available <= 0)
                {
                    current = current.Date.AddDays(1).AddHours(8);
                    continue;
                }

                var consumed = Math.Min(remaining, available);
                current = current.AddHours(consumed);
                remaining -= consumed;

                if (remaining > 0)
                {
                    current = current.Date.AddDays(1).AddHours(8);
                }
            }

            return current;
        }

        private async Task NotifyMaintenanceOrderReminderAsync(Ticket ticket, string reminderWindowLabel)
        {
            try
            {
                await _emailNotificationService.NotifyMaintenanceOrderReminderAsync(ticket, reminderWindowLabel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar alerta SLA de mantenimiento para ticket {TicketNumber}", ticket.TicketNumber);
            }
        }

        private async Task NotifyMaintenanceCostApprovalRequiredAsync(Ticket ticket)
        {
            try
            {
                await _emailNotificationService.NotifyMaintenanceOrderCostApprovalRequiredAsync(ticket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar alerta de aprobacion de costos para ticket {TicketNumber}", ticket.TicketNumber);
            }
        }

        private async Task NotifyNewTicketAsync(Ticket ticket)
        {
            try
            {
                await _emailNotificationService.NotifyNewTicketAsync(ticket, GetChangedBy());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar notificacion de correo para ticket {TicketNumber}", ticket.TicketNumber);
            }

            try
            {
                await _whatsAppNotificationService.NotifyNewTicketAsync(ticket, GetChangedBy());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar notificacion de WhatsApp para ticket {TicketNumber}", ticket.TicketNumber);
            }
        }
        private async Task NotifyTicketClosedAsync(Ticket ticket)
        {
            try
            {
                await _emailNotificationService.NotifyTicketClosedAsync(ticket, GetChangedBy());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar notificacion de cierre por correo para ticket {TicketNumber}", ticket.TicketNumber);
            }
        }
        private string GetChangedBy()
        {
            return User.Identity?.Name ?? User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value ?? "Sistema";
        }
    }
}





































