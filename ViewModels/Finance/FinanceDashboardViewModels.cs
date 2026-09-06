using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.ViewModels.Finance
{
    public sealed class FinanceDashboardViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal UsdToCordobaRate { get; set; }
        public int OpenRequests { get; set; }
        public int PendingApprovals { get; set; }
        public int ReturnedRequests { get; set; }
        public int EscalatedRequests { get; set; }
        public int InvoicesIssued { get; set; }
        public int ReceiptsIssued { get; set; }
        public int UnsignedDocuments { get; set; }
        public decimal InvoiceTotalCordoba { get; set; }
        public decimal ReceiptTotalCordoba { get; set; }
        public decimal BudgetTotalCordoba { get; set; }
        public decimal BudgetSpentCordoba { get; set; }
        public decimal BudgetAvailableCordoba { get; set; }
        public double BudgetUsagePercent { get; set; }
        public List<FinanceDashboardAlertViewModel> Alerts { get; set; } = new();
        public List<FinanceDashboardCostCenterRowViewModel> TopCostCenters { get; set; } = new();
        public List<FinanceDashboardDocumentRowViewModel> RecentDocuments { get; set; } = new();
        public List<FinanceDashboardAuditRowViewModel> RecentAudit { get; set; } = new();
    }

    public sealed class FinanceDashboardAlertViewModel
    {
        public string Tone { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
    }

    public sealed class FinanceDashboardCostCenterRowViewModel
    {
        public string Area { get; set; } = string.Empty;
        public string CostCenterNumber { get; set; } = string.Empty;
        public string CostCenterName { get; set; } = string.Empty;
        public decimal BudgetCordoba { get; set; }
        public decimal SpentCordoba { get; set; }
        public double UsagePercent { get; set; }
    }

    public sealed class FinanceDashboardDocumentRowViewModel
    {
        public string Type { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Counterparty { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "C$";
        public DateTime DateUtc { get; set; }
    }

    public sealed class FinanceDashboardAuditRowViewModel
    {
        public string EntityName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime PerformedAtUtc { get; set; }
        public string? Details { get; set; }
    }

    public sealed class FinanceCounterpartyEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(150)]
        [Display(Name = "Nombre")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        [Display(Name = "Tipo")]
        public string Type { get; set; } = "Proveedor";

        [MaxLength(40)]
        [Display(Name = "RUC / Identificacion")]
        public string? TaxId { get; set; }

        [MaxLength(40)]
        [Display(Name = "Telefono")]
        public string? Phone { get; set; }

        [MaxLength(120)]
        [EmailAddress]
        [Display(Name = "Correo")]
        public string? Email { get; set; }

        [MaxLength(300)]
        [Display(Name = "Direccion")]
        public string? Address { get; set; }

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;
    }

    public sealed class FinanceSettingsViewModel
    {
        [Range(typeof(decimal), "0.0001", "9999999999999999.9999")]
        [Display(Name = "Tipo de cambio USD a C$")]
        public decimal UsdToCordobaRate { get; set; } = 36.5m;

        [Range(1, 240)]
        [Display(Name = "Horas para escalamiento")]
        public int DefaultEscalationHours { get; set; } = 24;

        [Range(1, 100)]
        [Display(Name = "Umbral de alerta por defecto")]
        public int DefaultAlertThresholdPercent { get; set; } = 80;
    }
}
