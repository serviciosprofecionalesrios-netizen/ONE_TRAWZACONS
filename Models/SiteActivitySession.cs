using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Models;
[Index(nameof(UserId), nameof(StartedAtUtc))]
[Index(nameof(LastSeenAtUtc))]
public sealed class SiteActivitySession
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    [MaxLength(150)] public string Email { get; set; } = "";
    [MaxLength(120)] public string DisplayName { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime? LastActivityAtUtc { get; set; }
    [ConcurrencyCheck] public DateTime? LastPulseAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public long ActiveSeconds { get; set; }
    public bool IsVisible { get; set; }
    public bool HasRecentActivity { get; set; }
    [MaxLength(80)] public string LastController { get; set; } = "";
    [MaxLength(80)] public string LastAction { get; set; } = "";
}
