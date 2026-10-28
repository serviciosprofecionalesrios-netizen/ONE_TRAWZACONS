using System.ComponentModel.DataAnnotations;
namespace ITServiceDeskApp.Models;
public sealed class OperationalFleetDriver
{
    [Key, MaxLength(200)] public string EquipmentKey { get; set; } = "";
    [MaxLength(160)] public string EquipmentCode { get; set; } = "";
    public int DriverId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    [MaxLength(150)] public string ChangedBy { get; set; } = "";
}
