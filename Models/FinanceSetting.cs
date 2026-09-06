using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class FinanceSetting
    {
        public int Id { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Range(typeof(decimal), "0.0001", "9999999999999999.9999")]
        public decimal UsdToCordobaRate { get; set; } = 36.5m;

        [Range(1, 240)]
        public int DefaultEscalationHours { get; set; } = 24;

        [Range(1, 100)]
        public int DefaultAlertThresholdPercent { get; set; } = 80;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(120)]
        public string UpdatedBy { get; set; } = "Sistema";
    }
}
