using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.ViewModels.Maintenance;

public class ExternalRepairEditViewModel
{
    [Required]
    public string SourceRepairNumber { get; set; } = string.Empty;
    public string EquipmentCode { get; set; } = string.Empty;
    public string ProblemDescription { get; set; } = string.Empty;
    public string Status { get; set; } = "Pendiente";
    public string? AssignedMechanic { get; set; }
    public string? WorkPerformed { get; set; }
    public string? DeliveryDocumentPath { get; set; }
}
