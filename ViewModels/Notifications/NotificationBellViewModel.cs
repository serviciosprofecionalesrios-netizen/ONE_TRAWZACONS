using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels.Notifications
{
    public class NotificationBellViewModel
    {
        public int TotalCount { get; set; }
        public int DisplayCount => Math.Min(TotalCount, 99);
        public bool HasOverflow => TotalCount > 99;
        public List<NotificationItemViewModel> Items { get; set; } = new();
    }

    public class NotificationItemViewModel
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string PriorityLabel { get; set; } = string.Empty;
        public string PriorityCssClass { get; set; } = string.Empty;
        public string CardCssClass { get; set; } = string.Empty;
        public string IconCssClass { get; set; } = string.Empty;
        public string IconSymbolCssClass { get; set; } = string.Empty;
        public string TimeLabel { get; set; } = string.Empty;
        public DateTime ReferenceDateUtc { get; set; }
    }
}
