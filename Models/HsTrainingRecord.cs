using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class HsTrainingRecord
    {
        public int Id { get; set; }

        public DateTime TrainingDateUtc { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(160)]
        public string Topic { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Trainer { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Audience { get; set; } = "Personal operativo";

        [MaxLength(120)]
        public string? EmployeeName { get; set; }

        [MaxLength(60)]
        public string? EmployeeCode { get; set; }

        [MaxLength(120)]
        public string? Position { get; set; }

        [Range(0, 10000)]
        public int AttendeeCount { get; set; }

        public bool RequiredTraining { get; set; } = true;

        public DateTime? ExpirationDateUtc { get; set; }

        [MaxLength(40)]
        public string Result { get; set; } = "Aprobado";

        [MaxLength(500)]
        public string? EvidencePath { get; set; }

        [MaxLength(1500)]
        public string? Notes { get; set; }

        [MaxLength(120)]
        public string CreatedBy { get; set; } = "Sistema";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
