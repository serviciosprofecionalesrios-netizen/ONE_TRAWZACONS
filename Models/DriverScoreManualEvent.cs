using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models;

public class DriverScoreManualEvent
{
    public int Id { get; set; }
    [Required] public DateTime EventAt { get; set; }
    [Required, MaxLength(160)] public string Driver { get; set; } = string.Empty;
    [Required, MaxLength(40)] public string Vehicle { get; set; } = string.Empty;
    [Required, MaxLength(120)] public string EventType { get; set; } = string.Empty;
    [MaxLength(120)] public string? Group { get; set; }
    [MaxLength(80)] public string? Location { get; set; }
    [MaxLength(1000)] public string? Observation { get; set; }
    public bool TimelyManaged { get; set; }
    public bool CoachingCompleted { get; set; }
    [MaxLength(150)] public string? RegisteredBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
