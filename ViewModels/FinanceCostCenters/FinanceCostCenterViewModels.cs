using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.ViewModels.FinanceCostCenters
{
    public sealed class FinanceCostCenterIndexViewModel
    {
        public string PeriodPreset { get; set; } = "month";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? SelectedArea { get; set; }
        public string? SelectedCostCenterNumber { get; set; }

        public List<FinanceCostCenterSelectOptionViewModel> AreaOptions { get; set; } = new();
        public List<FinanceCostCenterSelectOptionViewModel> CostCenterOptions { get; set; } = new();

        public decimal TotalBudgetCordoba { get; set; }
        public decimal TotalSpentCordoba { get; set; }
        public decimal TotalAvailableCordoba { get; set; }
        public double UsagePercent { get; set; }
        public int NearThresholdCount { get; set; }
        public int OverBudgetCount { get; set; }
        public int OverBudgetRequestsCount { get; set; }

        public List<FinanceCostCenterAlertViewModel> Alerts { get; set; } = new();
        public List<FinanceCostCenterSummaryRowViewModel> Rows { get; set; } = new();
    }

    public sealed class FinanceCostCenterSummaryRowViewModel
    {
        public int Id { get; set; }
        public string Area { get; set; } = string.Empty;
        public string CostCenterNumber { get; set; } = string.Empty;
        public string CostCenterName { get; set; } = string.Empty;
        public decimal BudgetCordoba { get; set; }
        public decimal SpentCordoba { get; set; }
        public decimal AvailableCordoba { get; set; }
        public double UsagePercent { get; set; }
        public int AlertThresholdPercent { get; set; }
        public bool IsNearThreshold { get; set; }
        public bool IsOverBudget { get; set; }
        public int RequestCount { get; set; }
        public int OverBudgetRequestCount { get; set; }
        public bool HardStopOnOverrun { get; set; }
        public bool RequireAuthorizationOnOverrun { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class FinanceCostCenterAlertViewModel
    {
        public string Severity { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
    }

    public sealed class FinanceCostCenterSelectOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public sealed class FinanceCostCenterEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(120)]
        [Display(Name = "Area")]
        public string Area { get; set; } = string.Empty;

        [MaxLength(40)]
        [Display(Name = "#CC")]
        public string CostCenterNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Display(Name = "Nombre del Centro de Costo")]
        public string CostCenterName { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El presupuesto debe ser mayor o igual a 0.")]
        [Display(Name = "Presupuesto mensual (C$)")]
        public decimal MonthlyBudgetCordoba { get; set; }

        [Range(1, 100)]
        [Display(Name = "Umbral de alerta (%)")]
        public int AlertThresholdPercent { get; set; } = 80;

        [Display(Name = "Bloquear sobregiro")]
        public bool HardStopOnOverrun { get; set; } = false;

        [Display(Name = "Requiere autorizacion en sobregiro")]
        public bool RequireAuthorizationOnOverrun { get; set; } = true;

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;
    }
}
