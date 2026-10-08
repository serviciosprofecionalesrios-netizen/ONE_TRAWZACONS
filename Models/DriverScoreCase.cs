using System.ComponentModel.DataAnnotations;
namespace ITServiceDeskApp.Models;
public sealed class DriverScoreCase
{
    [Key, MaxLength(64)] public string EventKey { get; set; } = string.Empty;
    [MaxLength(30)] public string Status { get; set; } = "Pendiente";
    [MaxLength(150)] public string? Responsible { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public byte[]? Evidence { get; set; }
    [MaxLength(255)] public string? EvidenceName { get; set; }
    public DateTime UpdatedAt { get; set; }
    [MaxLength(150)] public string? UpdatedBy { get; set; }
}
