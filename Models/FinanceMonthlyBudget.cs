using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class FinanceMonthlyBudget
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string CostCenter { get; set; } = string.Empty;

        [Range(2000, 2100)]
        public int Year { get; set; }

        [Range(1, 12)]
        public int Month { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99")]
        public decimal BudgetLimitCordoba { get; set; }

        public bool HardStopOnOverrun { get; set; } = true;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
