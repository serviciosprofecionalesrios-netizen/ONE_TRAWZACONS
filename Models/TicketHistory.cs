using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class TicketHistory
    {
        public int Id { get; set; }

        public int TicketId { get; set; }

        public Ticket Ticket { get; set; } = null!;

        public DateTime ChangeDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string FieldChanged { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? OldValue { get; set; }

        [MaxLength(500)]
        public string? NewValue { get; set; }

        [MaxLength(100)]
        public string? ChangedBy { get; set; }
    }
}
