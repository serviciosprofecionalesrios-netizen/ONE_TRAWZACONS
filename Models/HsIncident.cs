using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class HsIncident
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string IncidentNumber { get; set; } = string.Empty;

        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Area { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string ReportedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(60)]
        public string IncidentType { get; set; } = "Condicion insegura";

        [Required]
        [MaxLength(40)]
        public string Severity { get; set; } = "Media";

        [Required]
        [MaxLength(40)]
        public string Status { get; set; } = "Abierto";

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(1500)]
        public string? ImmediateAction { get; set; }

        [MaxLength(1500)]
        public string? RootCause { get; set; }

        [MaxLength(1200)]
        public string? ImmediateCause { get; set; }

        [MaxLength(1200)]
        public string? FiveWhyAnalysis { get; set; }

        [MaxLength(800)]
        public string? Witnesses { get; set; }

        [MaxLength(120)]
        public string? Investigator { get; set; }

        public DateTime? InvestigationDueDateUtc { get; set; }

        public DateTime? InvestigationClosedAtUtc { get; set; }

        [Range(1, 5)]
        public int RiskProbability { get; set; } = 3;

        [Range(1, 5)]
        public int RiskSeverity { get; set; } = 3;

        [Range(1, 25)]
        public int RiskScore { get; set; } = 9;

        [MaxLength(40)]
        public string RiskLevel { get; set; } = "Medio";

        [Range(1, 5)]
        public int ResidualRiskProbability { get; set; } = 2;

        [Range(1, 5)]
        public int ResidualRiskSeverity { get; set; } = 2;

        [Range(1, 25)]
        public int ResidualRiskScore { get; set; } = 4;

        [MaxLength(40)]
        public string ResidualRiskLevel { get; set; } = "Bajo";

        [MaxLength(1500)]
        public string? CurrentControls { get; set; }

        [MaxLength(1500)]
        public string? RecommendedControls { get; set; }

        public bool LostTime { get; set; }

        [Range(0, 365)]
        public int LostDays { get; set; }

        [MaxLength(500)]
        public string? EvidencePath { get; set; }

        [MaxLength(120)]
        public string CreatedBy { get; set; } = "Sistema";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
