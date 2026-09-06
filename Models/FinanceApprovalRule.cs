using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class FinanceApprovalRule
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string? RequestType { get; set; }

        [MaxLength(120)]
        public string? CostCenter { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99")]
        public decimal MinAmountCordoba { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99")]
        public decimal? MaxAmountCordoba { get; set; }

        [Range(1, 5)]
        public int RequiredApprovals { get; set; } = 1;

        [Range(1, 240)]
        public int EscalationHours { get; set; } = 24;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
