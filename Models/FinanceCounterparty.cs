using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class FinanceCounterparty
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        public string Type { get; set; } = "Proveedor";

        [MaxLength(40)]
        public string? TaxId { get; set; }

        [MaxLength(40)]
        public string? Phone { get; set; }

        [MaxLength(120)]
        public string? Email { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
