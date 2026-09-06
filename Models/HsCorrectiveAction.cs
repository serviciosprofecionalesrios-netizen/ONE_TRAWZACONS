using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class HsCorrectiveAction
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(40)]
        public string SourceType { get; set; } = "Manual";

        public int? SourceId { get; set; }

        [Required]
        [MaxLength(180)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Owner { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        public string Priority { get; set; } = "Media";

        [Required]
        [MaxLength(40)]
        public string Status { get; set; } = "Abierta";

        public DateTime DueDateUtc { get; set; } = DateTime.UtcNow.Date.AddDays(7);

        public DateTime? ClosedAtUtc { get; set; }

        [MaxLength(1200)]
        public string? ClosureNotes { get; set; }

        [MaxLength(120)]
        public string? ReviewedBy { get; set; }

        public DateTime? ReviewedAtUtc { get; set; }

        [MaxLength(120)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        [MaxLength(1200)]
        public string? ApprovalNotes { get; set; }

        [MaxLength(120)]
        public string CreatedBy { get; set; } = "Sistema";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
