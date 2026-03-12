using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.Services.Interfaces
{
    public interface IEmailNotificationService
    {
        Task NotifyNewTicketAsync(Ticket ticket, string? createdBy, CancellationToken cancellationToken = default);
    }
}
