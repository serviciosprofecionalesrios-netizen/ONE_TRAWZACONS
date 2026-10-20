using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
namespace ITServiceDeskApp.Models;
[Index(nameof(RecordKey), IsUnique = true)]
public sealed class OperationalMasterRecord
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string Kind { get; set; } = "equipos";
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [MaxLength(30)] public string? Plate { get; set; }
    [Required, MaxLength(30)] public string Tenure { get; set; } = "";
    [Required, MaxLength(180)] public string Owner { get; set; } = "";
    [MaxLength(250), Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever] public string RecordKey { get; set; } = "";
    public bool IsActive { get; set; } = true;
    [MaxLength(250)] public string? SourceKey { get; set; }
    [MaxLength(40)] public string? FirstName { get; set; }
    [MaxLength(40)] public string? SecondName { get; set; }
    [MaxLength(40)] public string? FirstSurname { get; set; }
    [MaxLength(40)] public string? SecondSurname { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public string DisplayId => $"TW-{Id:D6}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
