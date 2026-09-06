using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ITServiceDeskApp.ViewModels.FinanceDocuments
{
    public class FinanceReceiptCreateViewModel
    {
        [Required]
        [MaxLength(100)]
        [Display(Name = "Sitio / Sede")]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Display(Name = "Recibido de")]
        public string ReceivedFrom { get; set; } = string.Empty;

        [Display(Name = "Contraparte guardada")]
        public int? CounterpartyId { get; set; }

        [MaxLength(40)]
        [Display(Name = "RUC / Identificacion")]
        public string? CounterpartyTaxId { get; set; }

        [Required]
        [MaxLength(1200)]
        [Display(Name = "Concepto")]
        public string Concept { get; set; } = string.Empty;

        [Required]
        [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "El monto debe ser mayor a 0.")]
        [Display(Name = "Monto")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(10)]
        [Display(Name = "Moneda")]
        public string Currency { get; set; } = "C$";

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de recibo")]
        public DateTime ReceiptDate { get; set; } = DateTime.Today;

        [MaxLength(700)]
        [Display(Name = "Notas")]
        public string? Notes { get; set; }

        [Display(Name = "Adjuntos")]
        public List<IFormFile> Attachments { get; set; } = new();
    }
}
