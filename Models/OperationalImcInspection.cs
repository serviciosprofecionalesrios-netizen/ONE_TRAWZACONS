using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Models;

[PrimaryKey(nameof(Month), nameof(UnitKey), nameof(PresentedAt), nameof(Color))]
public sealed class OperationalImcInspection
{
    [MaxLength(7)] public string Month { get; set; } = "";
    [MaxLength(160)] public string UnitKey { get; set; } = "";
    [MaxLength(160)] public string UnitCode { get; set; } = "";
    public DateTime PresentedAt { get; set; }
    [MaxLength(10)] public string Color { get; set; } = "";
    [ConcurrencyCheck] public string? FindingsJson { get; set; }
}
