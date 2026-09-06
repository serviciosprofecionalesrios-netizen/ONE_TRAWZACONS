using System;
using System.ComponentModel.DataAnnotations;
using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.ViewModels.Tickets
{
    public sealed class FinanceRequestCreateViewModel
    {
        [Required]
        [MaxLength(100)]
        [Display(Name = "Usuario Solicitante")]
        public string RequestingUser { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Display(Name = "Sitio / Sede")]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Display(Name = "Tipo de Solicitud")]
        public string RequestType { get; set; } = string.Empty;

        [Required]
        [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "El monto debe ser mayor a 0.")]
        [Display(Name = "Monto Solicitado")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(10)]
        [Display(Name = "Moneda")]
        public string Currency { get; set; } = "C$";

        [Required]
        [MaxLength(120)]
        [Display(Name = "Centro de Costo")]
        public string CostCenter { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha Requerida")]
        public DateTime NeededByDate { get; set; } = DateTime.Today;

        [MaxLength(150)]
        [Display(Name = "Proveedor / Beneficiario")]
        public string? VendorName { get; set; }

        [MaxLength(120)]
        [Display(Name = "Referencia (Factura/OC)")]
        public string? ReferenceNumber { get; set; }

        [MaxLength(80)]
        [Display(Name = "Documento Beneficiario (RUC/Cedula)")]
        public string? BeneficiaryDocument { get; set; }

        [MaxLength(60)]
        [Display(Name = "Metodo de Pago")]
        public string? PaymentMethod { get; set; }

        [MaxLength(120)]
        [Display(Name = "Cuenta Bancaria")]
        public string? BankAccount { get; set; }

        [MaxLength(500)]
        [Display(Name = "Uso (Solicitud OC)")]
        public string? OcUsage { get; set; }

        [MaxLength(1200)]
        [Display(Name = "Justificacion de requerimiento (Solicitud OC)")]
        public string? OcRequirementJustification { get; set; }

        [MaxLength(1200)]
        [Display(Name = "Justificacion de cantidades (Solicitud OC)")]
        public string? OcQuantityJustification { get; set; }

        [MaxLength(40)]
        [Display(Name = "Estado OC")]
        public string? OcStatus { get; set; } = "PENDIENTE";

        [Range(0, 200, ErrorMessage = "La cantidad de lineas debe estar entre 0 y 200.")]
        [Display(Name = "Cant. Lineas")]
        public int OcLineCount { get; set; }

        [MaxLength(2000)]
        [Display(Name = "Descripcion Articulos (Solicitud OC)")]
        public string? OcItemDescription { get; set; }

        [MaxLength(120)]
        [Display(Name = "Orden de Compra (Solicitud OC)")]
        public string? OcPurchaseOrderNumber { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Viatico desde")]
        public DateTime? TravelFromDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Viatico hasta")]
        public DateTime? TravelToDate { get; set; }

        [Required]
        [MinLength(15)]
        [MaxLength(1200)]
        [Display(Name = "Justificacion")]
        public string BusinessJustification { get; set; } = string.Empty;

        [MaxLength(700)]
        [Display(Name = "Notas Adicionales")]
        public string? AdditionalNotes { get; set; }

        [Required]
        [Display(Name = "Prioridad")]
        public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

        [MaxLength(20)]
        public string OcWorkflowStage { get; set; } = "request";

        public int? SourceOcTicketId { get; set; }
    }
}
