using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.Services.Interfaces
{
    public interface IEmailNotificationService
    {
        Task NotifyNewTicketAsync(Ticket ticket, string? createdBy, CancellationToken cancellationToken = default);
        Task NotifyTicketClosedAsync(Ticket ticket, string? closedBy, CancellationToken cancellationToken = default);
        Task NotifyMaintenanceOrderReminderAsync(Ticket ticket, string reminderWindowLabel, CancellationToken cancellationToken = default);
        Task NotifyMaintenanceOrderCostApprovalRequiredAsync(Ticket ticket, CancellationToken cancellationToken = default);
        Task NotifyMaintenanceAppointmentCreatedAsync(MaintenanceAppointment appointment, string? createdBy, CancellationToken cancellationToken = default);
        Task NotifyMaintenanceAppointmentReminderAsync(MaintenanceAppointment appointment, string reminderWindowLabel, CancellationToken cancellationToken = default);
    }
}
