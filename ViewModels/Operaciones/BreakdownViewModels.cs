using System.ComponentModel.DataAnnotations;
using ITServiceDeskApp.Models;
namespace ITServiceDeskApp.ViewModels.Operaciones;
public sealed class BreakdownCreateViewModel
{
    [Required] public DateTime? EventAt { get; set; } = DateTime.UtcNow.AddHours(-6);
    [Required,MaxLength(160)] public string Equipment { get; set; } = "";
    [MaxLength(160)] public string? Driver { get; set; }
    [Required,MaxLength(80)] public string Circuit { get; set; } = "Limón";
    [Required] public string EquipmentStatus { get; set; } = "";
    public List<BreakdownDamageInput> Damages { get; set; } = [];
    public List<int> TechnicianIds { get; set; } = [];
    public bool EquipmentUnderRepair { get; set; }
    public bool GondolaUnderRepair { get; set; }
    [MaxLength(4000)] public string? Observation { get; set; }
    public List<string> EquipmentOptions { get; set; } = [];
    public List<string> DriverOptions { get; set; } = [];
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public List<MaintenanceTechnician> Technicians { get; set; } = [];
}

public sealed class BreakdownDamageInput
{
    public string? Category { get; set; }
    public string? Element { get; set; }
    public string? Target { get; set; }
}
public sealed class BreakdownListViewModel
{
    public List<OperationalBreakdown> Records { get; set; } = [];
    public string? Search { get; set; }
    public int Page { get; set; }
    public int Total { get; set; }
    public bool CanManage { get; set; }
}
