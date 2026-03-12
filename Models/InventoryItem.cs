using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string AssetCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? AssetTag { get; set; }

        [MaxLength(100)]
        public string? SerialNumber { get; set; }

        [Required]
        [MaxLength(80)]
        public string Category { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? Subcategory { get; set; }

        [MaxLength(80)]
        public string? Brand { get; set; }

        [MaxLength(100)]
        public string? Model { get; set; }

        [MaxLength(400)]
        public string? TechnicalSpecifications { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Disponible";

        [Required]
        [MaxLength(50)]
        public string Condition { get; set; } = "Bueno";

        [MaxLength(120)]
        public string? Site { get; set; }

        [MaxLength(100)]
        public string? Department { get; set; }

        [MaxLength(120)]
        public string? AssignedTo { get; set; }

        [MaxLength(150)]
        public string? AssignedToEmail { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public DateTime? WarrantyEndDate { get; set; }

        [MaxLength(120)]
        public string? Supplier { get; set; }

        [MaxLength(80)]
        public string? InvoiceNumber { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo en C$ debe ser mayor o igual a 0.")]
        public decimal? CostCordoba { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo en $ debe ser mayor o igual a 0.")]
        public decimal? CostUsd { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(50)]
        public string? MacAddress { get; set; }

        [MaxLength(120)]
        public string? OperatingSystem { get; set; }

        [MaxLength(120)]
        public string? OfficeVersion { get; set; }

        [MaxLength(120)]
        public string? Antivirus { get; set; }

        public DateTime? LastMaintenanceDate { get; set; }

        public DateTime? NextMaintenanceDate { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
