using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.Services.Interfaces
{
    public interface IWhatsAppNotificationService
    {
        Task NotifyNewTicketAsync(Ticket ticket, string? createdBy, CancellationToken cancellationToken = default);
    }
}
