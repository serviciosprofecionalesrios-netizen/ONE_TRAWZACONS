using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels.Reports
{
    public sealed class ReportIndexViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string SelectedTechnician { get; set; } = "all";
        public string SelectedArea { get; set; } = "all";
        public string SelectedSite { get; set; } = "all";
        public string ActiveTab { get; set; } = "summary";
        public bool PrintMode { get; set; }

        public List<ReportSelectOptionViewModel> TechnicianOptions { get; set; } = new();
        public List<ReportSelectOptionViewModel> AreaOptions { get; set; } = new();
        public List<ReportSelectOptionViewModel> SiteOptions { get; set; } = new();

        public int TotalTickets { get; set; }
        public int CreatedTickets { get; set; }
        public int ClosedTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int InProgressTickets { get; set; }
        public int PendingTickets { get; set; }
        public int OverdueTickets { get; set; }
        public int NearDueTickets { get; set; }
        public int UnassignedTickets { get; set; }
        public int CriticalTickets { get; set; }
        public int ReopenedTickets { get; set; }
        public int BacklogDelta { get; set; }
        public int ActiveBacklog { get; set; }

        public double AverageResolutionHours { get; set; }
        public double AverageFirstResponseHours { get; set; }
        public int SatisfactionPercent { get; set; }
        public int SlaCompliancePercent { get; set; }
        public int CurrentPeriodTickets { get; set; }
        public int PreviousPeriodTickets { get; set; }
        public int CurrentYearTickets { get; set; }
        public int PreviousYearTickets { get; set; }
        public decimal TotalComponentCostCordoba { get; set; }
        public decimal TotalComponentCostUsd { get; set; }
        public decimal EstimatedLaborCostCordoba { get; set; }
        public decimal EstimatedTotalCostCordoba { get; set; }

        public List<ReportGroupItemViewModel> IncidentTypeDistribution { get; set; } = new();
        public List<ReportGroupItemViewModel> TicketsByTechnician { get; set; } = new();
        public List<ReportGroupItemViewModel> TicketsByArea { get; set; } = new();
        public List<ReportGroupItemViewModel> TicketsBySite { get; set; } = new();
        public bool IsFinanceArea { get; set; }
        public decimal FinanceTotalRequestedCordoba { get; set; }
        public decimal FinanceAverageRequestedCordoba { get; set; }
        public int FinanceRequestsWithBudgetRisk { get; set; }
        public List<ReportGroupItemViewModel> FinanceByRequester { get; set; } = new();
        public List<ReportGroupItemViewModel> FinanceByVendor { get; set; } = new();
        public List<ReportGroupItemViewModel> FinanceByCostCenter { get; set; } = new();
        public List<ReportGroupItemViewModel> FinanceByStatus { get; set; } = new();
        public List<ReportFinanceRequesterPerformanceViewModel> FinanceRequesterPerformance { get; set; } = new();
        public List<ReportTechnicianPerformanceViewModel> TechnicianPerformance { get; set; } = new();
        public List<ReportAlertViewModel> Alerts { get; set; } = new();
    }

    public sealed class ReportSelectOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public sealed class ReportGroupItemViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public sealed class ReportTechnicianPerformanceViewModel
    {
        public string Technician { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int OverdueTickets { get; set; }
        public int ReopenedTickets { get; set; }
        public double AverageResolutionHours { get; set; }
        public double SlaCompliancePercent { get; set; }
    }

    public sealed class ReportAlertViewModel
    {
        public string Severity { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
    }

    public sealed class ReportFinanceRequesterPerformanceViewModel
    {
        public string Requester { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int InReviewRequests { get; set; }
        public int CloseReadyRequests { get; set; }
        public int ClosedRequests { get; set; }
        public int BudgetRiskRequests { get; set; }
        public decimal RequestedAmountCordoba { get; set; }
    }
}
