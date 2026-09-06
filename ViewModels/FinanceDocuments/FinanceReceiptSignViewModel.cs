using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.ViewModels.FinanceDocuments
{
    public class FinanceReceiptSignViewModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        public int ReceiptId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string ReceivedFrom { get; set; } = string.Empty;
        public string Concept { get; set; } = string.Empty;
        public string Currency { get; set; } = "C$";
        public decimal Amount { get; set; }
        public DateTime ReceiptDate { get; set; }

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

