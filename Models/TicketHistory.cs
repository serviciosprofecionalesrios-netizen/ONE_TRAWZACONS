using System;

namespace ITServiceDeskApp.Models
{
    public class TicketHistory
    {
        public int Id { get; set; }

        public int TicketId { get; set; }

        // Relación con Ticket
        public Ticket Ticket { get; set; } = null!;

        public DateTime ChangeDate { get; set; } = DateTime.UtcNow;

        public string FieldChanged { get; set; } = string.Empty;
    }
}