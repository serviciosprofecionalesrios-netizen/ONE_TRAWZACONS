using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class HsInspection
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string InspectionNumber { get; set; } = string.Empty;

        public DateTime InspectionDateUtc { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Area { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? EquipmentCode { get; set; }

        [Required]
        [MaxLength(120)]
        public string Inspector { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        public string Category { get; set; } = "Inspeccion general";

        [Column(TypeName = "decimal(5,2)")]
        [Range(typeof(decimal), "0", "100")]
        public decimal ScorePercent { get; set; } = 100m;

        [Range(0, 1000)]
        public int FindingsCount { get; set; }

        [Range(0, 1000)]
        public int CriticalFindingsCount { get; set; }

        [Required]
        [MaxLength(40)]
        public string Status { get; set; } = "Completada";

        [MaxLength(2000)]
        public string? Notes { get; set; }

        [MaxLength(120)]
        public string CreatedBy { get; set; } = "Sistema";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
