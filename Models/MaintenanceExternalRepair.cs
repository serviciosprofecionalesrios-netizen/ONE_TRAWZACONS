using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models;

public class MaintenanceExternalRepair
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string SourceRepairNumber { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Status { get; set; } = "Pendiente";

    [MaxLength(150)]
    public string? AssignedMechanic { get; set; }

    [MaxLength(2000)]
    public string? WorkPerformed { get; set; }

    [MaxLength(500)]
    public string? DeliveryDocumentPath { get; set; }

    [MaxLength(120)]
    public string? UpdatedBy { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
