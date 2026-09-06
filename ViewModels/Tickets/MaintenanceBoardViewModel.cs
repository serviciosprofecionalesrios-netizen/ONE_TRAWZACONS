using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels.Tickets
{
    public sealed class MaintenanceBoardViewModel
    {
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
        public string? SelectedSite { get; set; }
        public string? SelectedTechnician { get; set; }
        public string? SelectedStage { get; set; }

        public int ActiveOrders { get; set; }
        public int OverdueOrders { get; set; }
        public int NearDueOrders { get; set; }
        public int UnassignedOrders { get; set; }

        public IReadOnlyList<string> SiteOptions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> TechnicianOptions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> StageOptions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<MaintenanceAirportBoardItem> Rows { get; set; } = Array.Empty<MaintenanceAirportBoardItem>();
    }
}
