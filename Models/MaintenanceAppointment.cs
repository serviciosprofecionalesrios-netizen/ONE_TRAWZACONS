using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class MaintenanceAppointment
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string AppointmentNumber { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime ScheduledFor { get; set; }

        [Required]
        [MaxLength(120)]
        public string RequestingUser { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string Tenencia { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        public string MaintenanceType { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string AssetOrArea { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? AssignedTechnician { get; set; }

        [MaxLength(60)]
        public string? TechnicianCategory { get; set; }

        [MaxLength(300)]
        public string? RecipientUsers { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Programada";

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
