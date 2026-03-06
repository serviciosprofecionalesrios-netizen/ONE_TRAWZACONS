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
        public int ResolvedTickets { get; set; }
        public int InProgressTickets { get; set; }
        public int PendingTickets { get; set; }
        public int OverdueTickets { get; set; }

        public double AverageResolutionHours { get; set; }
        public int SatisfactionPercent { get; set; }
        public int SlaCompliancePercent { get; set; }

        public List<ReportGroupItemViewModel> IncidentTypeDistribution { get; set; } = new();
        public List<ReportGroupItemViewModel> TicketsByTechnician { get; set; } = new();
        public List<ReportGroupItemViewModel> TicketsByArea { get; set; } = new();
        public List<ReportGroupItemViewModel> TicketsBySite { get; set; } = new();
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
}
