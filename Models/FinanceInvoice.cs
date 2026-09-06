using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class FinanceInvoice
    {
        public int Id { get; set; }

        public int? CounterpartyId { get; set; }

        [Required]
        [MaxLength(30)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string CounterpartyName { get; set; } = string.Empty;

        [MaxLength(40)]
        public string? CounterpartyTaxId { get; set; }

        [Required]
        [MaxLength(1200)]
        public string Concept { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "El monto debe ser mayor a 0.")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(10)]
        public string Currency { get; set; } = "C$";

        public DateTime InvoiceDateUtc { get; set; } = DateTime.UtcNow.Date;

        [MaxLength(700)]
        public string? Notes { get; set; }

        [MaxLength(120)]
        public string CreatedBy { get; set; } = "Sistema";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(40)]
        public string SignatureToken { get; set; } = Guid.NewGuid().ToString("N");

        [MaxLength(120)]
        public string? SignedByName { get; set; }

        public DateTime? SignedAtUtc { get; set; }

        [MaxLength(300)]
        public string? SignatureDeviceInfo { get; set; }

        public string? SignatureDataUrl { get; set; }

        [Required]
        [MaxLength(40)]
        public string ApprovalStatus { get; set; } = "Pendiente";

        [MaxLength(120)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        [MaxLength(700)]
        public string? ApprovalNotes { get; set; }

        public FinanceCounterparty? Counterparty { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
