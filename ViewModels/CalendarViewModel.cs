using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels
{
    public sealed class CalendarViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string CurrentMonthLabel { get; set; } = string.Empty;

        public int PreviousYear { get; set; }
        public int PreviousMonth { get; set; }
        public int NextYear { get; set; }
        public int NextMonth { get; set; }

        public string SelectedSla { get; set; } = "all";
        public string SelectedTechnician { get; set; } = "all";

        public List<CalendarSelectOptionViewModel> SlaOptions { get; set; } = new();
        public List<CalendarSelectOptionViewModel> TechnicianOptions { get; set; } = new();

        public int SlaCompliantCount { get; set; }
        public int SlaNearDueCount { get; set; }
        public int SlaBreachedCount { get; set; }
        public int CriticalPriorityCount { get; set; }

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
        public List<CalendarTicketBadgeViewModel> Tickets { get; set; } = new();
    }

    public sealed class CalendarTicketBadgeViewModel
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string SlaLabel { get; set; } = string.Empty;
        public string CssClass { get; set; } = string.Empty;
        public bool IsCritical { get; set; }
    }
}
