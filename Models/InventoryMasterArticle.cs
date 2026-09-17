using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    /// <summary>Catálogo de artículos equivalente a la tabla Maestro de AppSheet.</summary>
    public class InventoryMasterArticle
    {
        public int Id { get; set; }

        [Required, MaxLength(40)]
        public string ProductCode { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string Warehouse { get; set; } = string.Empty;

        [MaxLength(120)] public string? ItemType { get; set; }

        [Required, MaxLength(120)]
        public string Item { get; set; } = string.Empty;

        [MaxLength(120)] public string? PartNumber { get; set; }
        [MaxLength(500)] public string? Description { get; set; }
        [MaxLength(500)] public string? ExtraDescription { get; set; }
        [MaxLength(50)] public string? UnitOfMeasure { get; set; }
        [MaxLength(50)] public string? PurchaseUnitOfMeasure { get; set; }

        [Required, Range(0, 999999)]
        public int Shelf { get; set; }

        [MaxLength(100)] public string? Position { get; set; }

        [Range(0, 999999)]
        public int? SuggestedQuantity { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Timestamp] public byte[]? RowVersion { get; set; }
    }
}
