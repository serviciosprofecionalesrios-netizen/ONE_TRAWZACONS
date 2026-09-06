using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels.MaintenanceReports
{
    public sealed class MaintenanceReportsViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Site { get; set; }
        public string? FailureCategory { get; set; }

        public IReadOnlyList<string> SiteOptions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> FailureCategoryOptions { get; set; } = Array.Empty<string>();

        public int TotalMaintenanceTickets { get; set; }
        public int PreventiveTickets { get; set; }
        public int CorrectiveTickets { get; set; }
        public int OtherTickets { get; set; }

        public int FailureToFailureEvents { get; set; }
        public decimal? AverageKmBetweenFailures { get; set; }
        public decimal? MinKmBetweenFailures { get; set; }
        public decimal? MaxKmBetweenFailures { get; set; }

        public IReadOnlyList<FailureToFailureRow> FailureToFailureRows { get; set; } = Array.Empty<FailureToFailureRow>();
        public IReadOnlyList<DamagedElementRow> TopDamagedElements { get; set; } = Array.Empty<DamagedElementRow>();
        public IReadOnlyList<FailureCategorySummaryRow> FailureCategorySummary { get; set; } = Array.Empty<FailureCategorySummaryRow>();
        public IReadOnlyList<MaintenanceTypeSummaryRow> MaintenanceTypeSummary { get; set; } = Array.Empty<MaintenanceTypeSummaryRow>();
    }

    public sealed class FailureToFailureRow
    {
        public string UnitCode { get; set; } = string.Empty;
        public string PreviousTicketNumber { get; set; } = string.Empty;
        public DateTime PreviousRepairDate { get; set; }
        public decimal PreviousExitKm { get; set; }
        public string CurrentTicketNumber { get; set; } = string.Empty;
        public DateTime CurrentFailureDate { get; set; }
        public decimal CurrentFailureKm { get; set; }
        public decimal KmBetweenFailures { get; set; }
        public int DaysBetweenFailures { get; set; }
    }

    public sealed class DamagedElementRow
    {
        public string FailureCategory { get; set; } = string.Empty;
        public string DamagedElement { get; set; } = string.Empty;
        public int Total { get; set; }
    }

    public sealed class FailureCategorySummaryRow
    {
        public string FailureCategory { get; set; } = string.Empty;
        public int Total { get; set; }
    }

    public sealed class MaintenanceTypeSummaryRow
    {
        public string MaintenanceType { get; set; } = string.Empty;
        public int Total { get; set; }
        public double AverageResolutionHours { get; set; }
    }
}
