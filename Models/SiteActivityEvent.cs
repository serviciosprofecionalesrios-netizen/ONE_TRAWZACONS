using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Models;
[Index(nameof(OccurredAtUtc))]
[Index(nameof(UserId), nameof(OccurredAtUtc))]
public sealed class SiteActivityEvent
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public int UserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [MaxLength(30)] public string Kind { get; set; } = "";
    [MaxLength(80)] public string Controller { get; set; } = "";
    [MaxLength(80)] public string Action { get; set; } = "";
    [MaxLength(140)] public string Title { get; set; } = "";
    public int ResponseStatus { get; set; }
}
