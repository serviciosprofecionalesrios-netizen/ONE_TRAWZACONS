using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels
{
    public sealed class CalendarViewModel
    {
        public string Scope { get; set; } = "it";
        public bool IsMaintenanceScope { get; set; }

        public int Year { get; set; }
        public int Month { get; set; }
        public string CurrentMonthLabel { get; set; } = string.Empty;

        public int PreviousYear { get; set; }
        public int PreviousMonth { get; set; }
        public int NextYear { get; set; }
        public int NextMonth { get; set; }

        public string SelectedSla { get; set; } = "all";
        public string SelectedTechnician { get; set; } = "all";
        public string SelectedQuick { get; set; } = "all";

        public List<CalendarSelectOptionViewModel> SlaOptions { get; set; } = new();
        public List<CalendarSelectOptionViewModel> TechnicianOptions { get; set; } = new();

        public int TotalTicketsCount { get; set; }
        public int SlaCompliantCount { get; set; }
        public int SlaNearDueCount { get; set; }
        public int SlaBreachedCount { get; set; }
        public int UnassignedCount { get; set; }
        public int CriticalPriorityCount { get; set; }
        public int MyLoadCount { get; set; }

        public int MaintenanceAppointmentsCount { get; set; }
        public int MaintenanceActiveEquipmentCount { get; set; }
        public int MaintenanceEnteredThisMonthCount { get; set; }
        public int MaintenanceClosedThisMonthCount { get; set; }
        public int MaintenanceInWorkshopFromDateCount { get; set; }

        public DateTime? SelectedDate { get; set; }
        public string? SelectedDateIso { get; set; }
        public List<CalendarMaintenanceDateItemViewModel> SelectedDateAppointments { get; set; } = new();
        public List<CalendarMaintenanceDateItemViewModel> SelectedDateEquipment { get; set; } = new();

        public List<CalendarTechnicianLoadViewModel> TechnicianLoads { get; set; } = new();
        public List<CalendarDayViewModel> Days { get; set; } = new();
    }

    public sealed class CalendarSelectOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public sealed class CalendarDayViewModel
    {
        public DateTime Date { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        public bool IsSelected { get; set; }
        public int AppointmentsCount { get; set; }
        public int EquipmentCount { get; set; }
        public List<CalendarTicketBadgeViewModel> Tickets { get; set; } = new();
    }

    public sealed class CalendarTicketBadgeViewModel
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string SlaLabel { get; set; } = string.Empty;
        public string CssClass { get; set; } = string.Empty;
        public string DetailUrl { get; set; } = "#";
        public string ItemTypeLabel { get; set; } = "Ticket";
        public bool IsCritical { get; set; }
    }

    public sealed class CalendarTechnicianLoadViewModel
    {
        public string Technician { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int BreachedTickets { get; set; }
        public int CriticalTickets { get; set; }
    }

    public sealed class CalendarMaintenanceDateItemViewModel
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string EquipmentOrArea { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string? Technician { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DaysInWorkshop { get; set; }
        public string DetailUrl { get; set; } = "#";
        public string SourceType { get; set; } = string.Empty;
    }
}
