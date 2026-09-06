using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class HsWorkPermit
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string PermitNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        public string PermitType { get; set; } = "Trabajo en caliente";

        [Required]
        [MaxLength(100)]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Area { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string RequestedBy { get; set; } = string.Empty;

        public DateTime StartAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime EndAtUtc { get; set; } = DateTime.UtcNow.AddHours(8);

        [Required]
        [MaxLength(40)]
        public string Status { get; set; } = "Solicitado";

        [MaxLength(120)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        public DateTime? ApprovalResponseDueAtUtc { get; set; }

        [MaxLength(1500)]
        public string? Controls { get; set; }

        public bool HasRiskAssessment { get; set; }

        public bool HasAreaIsolation { get; set; }

        public bool HasPpe { get; set; }

        public bool HasEmergencyPlan { get; set; }

        public bool HasSupervisorApproval { get; set; }

        [MaxLength(1200)]
        public string? ChecklistNotes { get; set; }

        [MaxLength(2000)]
        public string? EvidencePaths { get; set; }

        [MaxLength(120)]
        public string CreatedBy { get; set; } = "Sistema";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
