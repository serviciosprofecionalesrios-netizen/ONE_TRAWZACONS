using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class FinanceAuditLog
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(80)]
        public string EntityName { get; set; } = string.Empty;

        public int? EntityId { get; set; }

        [Required]
        [MaxLength(80)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(120)]
        public string PerformedBy { get; set; } = "Sistema";

        public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(2000)]
        public string? Details { get; set; }
    }
}
