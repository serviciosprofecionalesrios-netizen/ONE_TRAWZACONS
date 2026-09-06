using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class MaintenanceTechnician
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(60)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        public string Shift { get; set; } = "Diurno";

        [Range(1, 50)]
        public int MaxActiveOrders { get; set; } = 4;

        [Required]
        [MaxLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string EmergencyContactName { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        public string EmergencyRelationship { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string EmergencyContactPhone { get; set; } = string.Empty;

        public bool IsAvailable { get; set; } = true;

        public bool IsActive { get; set; } = true;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
