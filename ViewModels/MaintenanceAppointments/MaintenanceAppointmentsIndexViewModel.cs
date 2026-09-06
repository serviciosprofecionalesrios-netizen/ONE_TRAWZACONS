using System;
using System.Collections.Generic;
using ITServiceDeskApp.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ITServiceDeskApp.ViewModels.MaintenanceAppointments
{
    public class MaintenanceAppointmentsIndexViewModel
    {
        public string? SelectedStatus { get; set; }
        public string? SelectedTechnician { get; set; }
        public string? SelectedSite { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public List<SelectListItem> StatusFilterOptions { get; set; } = new();
        public List<SelectListItem> TechnicianFilterOptions { get; set; } = new();
        public List<SelectListItem> SiteFilterOptions { get; set; } = new();

        public List<MaintenanceAppointmentListItemViewModel> Rows { get; set; } = new();

        public int TotalCount { get; set; }
        public int TodayCount { get; set; }
        public int OverdueCount { get; set; }
        public int UnassignedCount { get; set; }
        public int Reminder24hPendingCount { get; set; }
        public int Reminder2hPendingCount { get; set; }
        public int CompletedCount { get; set; }
        public double OnTimeRate { get; set; }
    }

    public class MaintenanceAppointmentListItemViewModel
    {
        public MaintenanceAppointment Appointment { get; set; } = new();
        public MaintenanceAppointmentMetadata Metadata { get; set; } = new();
        public string UserNotes { get; set; } = string.Empty;

        public bool IsOverdue { get; set; }
        public bool IsToday { get; set; }
        public bool IsTomorrow { get; set; }
        public bool Reminder24hPending { get; set; }
        public bool Reminder2hPending { get; set; }
        public bool HasAssignedTechnician { get; set; }
        public bool IsCompleted { get; set; }

        public string ProximityLabel { get; set; } = "Programada";
        public string ProximityClass { get; set; } = "secondary";
        public int ChecklistProgressPercent { get; set; }
    }
}
