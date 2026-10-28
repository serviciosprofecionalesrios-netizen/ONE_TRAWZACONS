using System.ComponentModel.DataAnnotations;
namespace ITServiceDeskApp.Models;
public sealed class OperationalFleetState
{
    [Key, MaxLength(200)] public string EquipmentKey { get; set; } = "";
    [Required, MaxLength(160)] public string EquipmentCode { get; set; } = "";
    [Required, MaxLength(30)] public string Status { get; set; } = "";
    [MaxLength(50)] public string? Reason { get; set; }
    public DateTime? SourceUploadedAt { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    [MaxLength(150)] public string ChangedBy { get; set; } = "";
}
