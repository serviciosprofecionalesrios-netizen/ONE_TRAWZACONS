using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class Credential
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? EnvironmentOrLocation { get; set; }

        [MaxLength(200)]
        public string? AccessUrlOrHost { get; set; }

        [MaxLength(80)]
        public string? Port { get; set; }

        [Required]
        [MaxLength(120)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Secret { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
