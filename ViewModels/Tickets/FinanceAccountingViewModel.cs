using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.ViewModels.Tickets
{
    public sealed class FinanceAccountingViewModel
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty;
        public string CostCenter { get; set; } = string.Empty;

        [Display(Name = "Asiento contable")]
        [MaxLength(80)]
        public string? AccountingEntryNumber { get; set; }

        [Display(Name = "Orden de compra")]
        [MaxLength(80)]
        public string? PurchaseOrderNumber { get; set; }

        [Display(Name = "Factura")]
        [MaxLength(80)]
        public string? InvoiceNumber { get; set; }

        [Display(Name = "Fecha de pago")]
        [DataType(DataType.Date)]
        public DateTime? PaymentDate { get; set; }

        [Display(Name = "Metodo de pago final")]
        [MaxLength(60)]
        public string? FinalPaymentMethod { get; set; }

        [Display(Name = "Estado de conciliacion")]
        [MaxLength(40)]
        public string? ReconciliationStatus { get; set; }

        [Display(Name = "Referencia ERP")]
        [MaxLength(120)]
        public string? ErpExportReference { get; set; }
    }
}
