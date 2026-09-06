using System;

namespace ITServiceDeskApp.ViewModels.MaintenanceAppointments
{
    public class MaintenanceAppointmentAuditEntryViewModel
    {
        public int Id { get; set; }
        public DateTime ChangeDateLocal { get; set; }
        public string FieldChanged { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string ChangedBy { get; set; } = "Sistema";
        public string? ChangeReason { get; set; }
    }
}
