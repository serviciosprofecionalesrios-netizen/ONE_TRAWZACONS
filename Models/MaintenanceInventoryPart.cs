using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class MaintenanceInventoryPart
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string PartCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(180)]
        public string PartName { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        public string Category { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? Subcategory { get; set; }

        [Required]
        [MaxLength(80)]
        public string SystemType { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string EquipmentType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? CompatibleModel { get; set; }

        [MaxLength(120)]
        public string? Brand { get; set; }

        [MaxLength(120)]
        public string? ManufacturerPartNumber { get; set; }

        [MaxLength(80)]
        public string? ItemCode { get; set; }

        [Required]
        [MaxLength(100)]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string Tenencia { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string UnitOfMeasure { get; set; } = "Unidad";

        [Range(0, 1000000, ErrorMessage = "La cantidad en stock debe ser mayor o igual a 0.")]
        public int QuantityOnHand { get; set; }

        [Range(0, 1000000, ErrorMessage = "El stock minimo debe ser mayor o igual a 0.")]
        public int MinimumStock { get; set; }

        [Range(0, 1000000, ErrorMessage = "Las salidas acumuladas deben ser mayor o igual a 0.")]
        public int QuantityIssued { get; set; }

        [Range(0, 1000000, ErrorMessage = "La cantidad en transito debe ser mayor o igual a 0.")]
        public int QuantityInTransit { get; set; }

        public DateTime? LastIssueDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string StockStatus { get; set; } = "Disponible";

        [MaxLength(120)]
        public string? Supplier { get; set; }

        [MaxLength(80)]
        public string? InvoiceNumber { get; set; }

        public DateTime? PurchaseDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo unitario en C$ debe ser mayor o igual a 0.")]
        public decimal? UnitCostCordoba { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo unitario en $ debe ser mayor o igual a 0.")]
        public decimal? UnitCostUsd { get; set; }

        [MaxLength(80)]
        public string? ShelfLocation { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
