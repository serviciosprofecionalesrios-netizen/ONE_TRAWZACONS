using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class FinanceCostCenter
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Area { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        public string CostCenterNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string CostCenterName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99")]
        public decimal MonthlyBudgetCordoba { get; set; }

        [Range(1, 100)]
        public int AlertThresholdPercent { get; set; } = 80;

        public bool HardStopOnOverrun { get; set; } = false;
        public bool RequireAuthorizationOnOverrun { get; set; } = true;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

