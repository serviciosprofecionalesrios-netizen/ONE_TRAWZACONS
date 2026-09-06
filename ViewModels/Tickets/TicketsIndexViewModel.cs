using System;
using System.Collections.Generic;
using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.ViewModels.Tickets
{
    public sealed class TicketsIndexViewModel
    {
        public string? Search { get; set; }
        public TicketStatus? Status { get; set; }
        public PriorityLevel? Priority { get; set; }
        public string? QuickFilter { get; set; }
        public string? Department { get; set; }
        public bool IsMaintenanceView { get; set; }
        public bool IsFinanceView { get; set; }
        public bool IsMineFinanceView { get; set; }
        public string? SelectedSite { get; set; }
        public string? SelectedTenencia { get; set; }
        public string? SelectedAssignedTechnician { get; set; }
        public string? SelectedIncidentType { get; set; }
        public string? SelectedMaintenanceStage { get; set; }
        public string? SelectedOverdueBand { get; set; }
        public string MaintenanceViewMode { get; set; } = "overview";
        public int FinanceOpenCount { get; set; }
        public int FinanceInProgressCount { get; set; }
        public int FinanceResolvedCount { get; set; }
        public int FinanceClosedCount { get; set; }
        public int FinancePendingApprovalCount { get; set; }
        public int FinanceRejectedCount { get; set; }
        public int FinanceReturnedCount { get; set; }
        public int FinanceEscalatedCount { get; set; }
        public int FinanceOverBudgetRequests { get; set; }
        public decimal FinanceBudgetLimitCordoba { get; set; }
        public decimal FinanceBudgetCommittedCordoba { get; set; }
        public double FinanceBudgetUsagePercent { get; set; }
        public decimal FinanceApprovedAmountCurrentMonthCordoba { get; set; }
        public double FinanceAverageCycleHours { get; set; }

        public int TotalTickets { get; set; }
        public int ActiveTickets { get; set; }
        public int OverdueTickets { get; set; }
        public int NearDueTickets { get; set; }
        public int UnassignedActiveTickets { get; set; }
        public int CriticalActiveTickets { get; set; }
        public double SlaComplianceRate { get; set; }

        public int CreatedLast7Days { get; set; }
        public int ClosedLast7Days { get; set; }
        public int BacklogDelta7Days => CreatedLast7Days - ClosedLast7Days;
        public int PreventiveOrders { get; set; }
        public int CorrectiveOrders { get; set; }
        public int PendingSparePartOrders { get; set; }
        public int ComponentChangeOrders { get; set; }
        public int ClosedTodayOrders { get; set; }
        public double MttrHours { get; set; }
        public int PendingCostApprovalOrders { get; set; }
        public decimal TotalMaintenanceCostCordoba { get; set; }
        public decimal TotalMaintenanceCostUsd { get; set; }
        public decimal AverageMaintenanceCostCordoba { get; set; }
        public decimal AverageMaintenanceCostUsd { get; set; }
        public int OrdersWithTrackedCost { get; set; }
        public int InventoryLowStockCount { get; set; }
        public int InventoryOutOfStockCount { get; set; }
        public int InventoryInTransitUnits { get; set; }
        public string InventoryHealthTone { get; set; } = "success";
        public string InventoryHealthLabel { get; set; } = "Stock estable";
        public int OverdueBand0To24Count { get; set; }
        public int OverdueBand24To72Count { get; set; }
        public int OverdueBandOver72Count { get; set; }

        public int TotalItems { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalPages { get; set; } = 1;
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;

        public IReadOnlyList<string> TechnicianOptions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> SiteOptions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> TenenciaOptions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> IncidentTypeOptions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> MaintenanceStageOptions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<Ticket> Tickets { get; set; } = Array.Empty<Ticket>();
        public IReadOnlyList<TechnicianLoadItem> TechnicianLoads { get; set; } = Array.Empty<TechnicianLoadItem>();
        public IReadOnlyList<TechnicianCapacityKpiItem> TechnicianCapacityItems { get; set; } = Array.Empty<TechnicianCapacityKpiItem>();
        public double TechnicianCapacityUtilizationRate { get; set; }
        public IReadOnlyList<MaintenanceStageKpiItem> StageKpis { get; set; } = Array.Empty<MaintenanceStageKpiItem>();
        public IReadOnlyList<MaintenanceSiteCostRankItem> MaintenanceSiteCostRanking { get; set; }
            = Array.Empty<MaintenanceSiteCostRankItem>();
        public IReadOnlyList<MaintenanceReincidenceItem> MaintenanceReincidenceRanking { get; set; }
            = Array.Empty<MaintenanceReincidenceItem>();
        public IReadOnlyDictionary<int, TicketAuditItem> LatestAuditByTicket { get; set; }
            = new Dictionary<int, TicketAuditItem>();
        public IReadOnlyList<MaintenanceOrderDetailItem> MaintenanceDetailRows { get; set; }
            = Array.Empty<MaintenanceOrderDetailItem>();
        public IReadOnlyList<MaintenanceAirportBoardItem> MaintenanceBoardRows { get; set; }
            = Array.Empty<MaintenanceAirportBoardItem>();
        public DateTime DashboardGeneratedAtUtc { get; set; } = DateTime.UtcNow;

        public IReadOnlyList<string> TrendLabels { get; set; } = Array.Empty<string>();
        public IReadOnlyList<int> TrendCreated { get; set; } = Array.Empty<int>();
        public IReadOnlyList<int> TrendClosed { get; set; } = Array.Empty<int>();
    }

    public sealed class TechnicianLoadItem
    {
        public string Technician { get; set; } = "Sin asignar";
        public int ActiveTickets { get; set; }
        public int OverdueTickets { get; set; }
        public int NearDueTickets { get; set; }
        public int OnTimeTickets { get; set; }
        public int CriticalTickets { get; set; }
        public double SlaOnTimeRate => ActiveTickets == 0
            ? 0
            : Math.Round((double)OnTimeTickets / ActiveTickets * 100, 1);
    }

    public sealed class TicketAuditItem
    {
        public int TicketId { get; set; }
        public string FieldChanged { get; set; } = string.Empty;
        public string FieldLabel { get; set; } = string.Empty;
        public string? NewValue { get; set; }
        public string ChangedBy { get; set; } = "Sistema";
        public DateTime ChangeDate { get; set; }
    }

    public sealed class TechnicianCapacityKpiItem
    {
        public string Technician { get; set; } = "Sin tecnico";
        public string Category { get; set; } = "Sin categoria";
        public string Shift { get; set; } = "N/A";
        public int ActiveOrders { get; set; }
        public int Capacity { get; set; }
        public double UtilizationRate { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsOverCapacity { get; set; }
    }

    public sealed class MaintenanceStageKpiItem
    {
        public string Stage { get; set; } = "Sin etapa";
        public int Total { get; set; }
    }

    public sealed class MaintenanceSiteCostRankItem
    {
        public string Site { get; set; } = "Sin sitio";
        public int Orders { get; set; }
        public decimal TotalCostCordoba { get; set; }
        public decimal TotalCostUsd { get; set; }
        public decimal AverageCostCordoba { get; set; }
        public decimal AverageCostUsd { get; set; }
        public int ReincidenceOrders { get; set; }
    }

    public sealed class MaintenanceReincidenceItem
    {
        public string UnitCode { get; set; } = "Sin unidad";
        public int Orders { get; set; }
        public int ReincidenceCount { get; set; }
        public string TopFailureCategory { get; set; } = "Sin categoria";
        public string MainSite { get; set; } = "Sin sitio";
    }

    public sealed class MaintenanceOrderDetailItem
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public DateTime CreatedDateUtc { get; set; }
        public DateTime SlaDeadlineUtc { get; set; }
        public string Site { get; set; } = string.Empty;
        public string UnitCode { get; set; } = string.Empty;
        public string IncidentType { get; set; } = string.Empty;
        public string FailureCategory { get; set; } = string.Empty;
        public string MaintenanceStage { get; set; } = string.Empty;
        public string AssignedTechnician { get; set; } = string.Empty;
        public TicketStatus Status { get; set; }
        public bool RequiresCostApproval { get; set; }
        public bool CostApproved { get; set; }
        public decimal TotalCostCordoba { get; set; }
        public decimal TotalCostUsd { get; set; }
        public double RemainingHoursToSla { get; set; }
    }

    public sealed class MaintenanceAirportBoardItem
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Technician { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string UnitCode { get; set; } = string.Empty;
        public string IncidentType { get; set; } = string.Empty;
        public string MaintenanceStage { get; set; } = string.Empty;
        public TicketStatus Status { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime SlaDeadlineUtc { get; set; }
        public double ElapsedHours { get; set; }
        public double RemainingHoursToSla { get; set; }
        public string SemaphoreLabel { get; set; } = "En tiempo";
        public string SemaphoreTone { get; set; } = "success";
        public int SemaphoreRank { get; set; }
    }
}
