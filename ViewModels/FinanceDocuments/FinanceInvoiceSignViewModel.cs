using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.ViewModels.FinanceDocuments
{
    public class FinanceInvoiceSignViewModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string CounterpartyName { get; set; } = string.Empty;
        public string Concept { get; set; } = string.Empty;
        public string Currency { get; set; } = "C$";
        public decimal Amount { get; set; }
        public DateTime InvoiceDate { get; set; }

        public bool AlreadySigned { get; set; }
        public string? SignedByName { get; set; }
        public DateTime? SignedAtLocal { get; set; }

        [Required]
        [MaxLength(120)]
        [Display(Name = "Nombre de quien firma")]
        public string SignerName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Firma")]
        public string SignatureDataUrl { get; set; } = string.Empty;
    }
}

