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
    [Required] public string Category { get; set; } = "";
    [Required] public string Element { get; set; } = "";
    [Required] public string DamageTarget { get; set; } = "";
    [MaxLength(4000)] public string? Observation { get; set; }
    public List<string> EquipmentOptions { get; set; } = [];
    public List<string> DriverOptions { get; set; } = [];
}
public sealed class BreakdownListViewModel
{
    public List<OperationalBreakdown> Records { get; set; } = [];
    public string? Search { get; set; }
    public int Page { get; set; }
    public int Total { get; set; }
    public bool CanManage { get; set; }
}
