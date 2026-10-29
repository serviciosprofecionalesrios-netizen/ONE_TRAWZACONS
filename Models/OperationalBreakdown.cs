using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Models;
[Index(nameof(SourceKey), IsUnique = true)]
public sealed class OperationalBreakdown
{
    public int Id { get; set; }
    [MaxLength(100)] public string? SourceKey { get; set; }
    public DateTime EventAt { get; set; }
    [Required, MaxLength(160)] public string Equipment { get; set; } = "";
    [MaxLength(160)] public string? Driver { get; set; }
    [Required, MaxLength(80)] public string Circuit { get; set; } = "Limón";
    [Required, MaxLength(80)] public string EquipmentStatus { get; set; } = "";
    [Required, MaxLength(80)] public string Category { get; set; } = "";
    [Required, MaxLength(200)] public string Element { get; set; } = "";
    [Required, MaxLength(80)] public string DamageTarget { get; set; } = "";
    [MaxLength(4000)] public string? Observation { get; set; }
    [MaxLength(80)] public string Status { get; set; } = "Abierta";
    public DateTime? EndAt { get; set; }
    public string? SourceJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(150)] public string RegisteredBy { get; set; } = "";
}
