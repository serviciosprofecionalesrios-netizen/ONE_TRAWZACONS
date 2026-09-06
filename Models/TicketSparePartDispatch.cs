using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class TicketSparePartDispatch
    {
        public int Id { get; set; }

        public int TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        public int MaintenanceInventoryPartId { get; set; }
        public MaintenanceInventoryPart MaintenanceInventoryPart { get; set; } = null!;

        [Required]
        [MaxLength(30)]
        public string PartCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(180)]
        public string PartName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? UnitOfMeasure { get; set; }

        [MaxLength(80)]
        public string? ItemCode { get; set; }

        [MaxLength(120)]
        public string? PartNumber { get; set; }

        [Range(1, 1000000, ErrorMessage = "La cantidad despachada debe ser mayor a 0.")]
        public int QuantityDispatched { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo unitario en C$ debe ser mayor o igual a 0.")]
        public decimal? UnitCostCordoba { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo total en C$ debe ser mayor o igual a 0.")]
        public decimal? TotalCostCordoba { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo unitario en $ debe ser mayor o igual a 0.")]
        public decimal? UnitCostUsd { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo total en $ debe ser mayor o igual a 0.")]
        public decimal? TotalCostUsd { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
