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
    [ConcurrencyCheck] public DateTime? EndAt { get; set; }
    public int? AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianIds { get; set; }
    public List<OperationalBreakdownDamage> Damages { get; set; } = [];
    [ConcurrencyCheck] public bool EquipmentUnderRepair { get; set; }
    [ConcurrencyCheck] public bool GondolaUnderRepair { get; set; }
    public DateTime? SlaDeadline { get; set; }
    public string? SourceJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(150)] public string RegisteredBy { get; set; } = "";

    public int[] GetTechnicianIds() => AssignedTechnicianIds is { Length: > 0 }
        ? AssignedTechnicianIds.Split(',').Select(int.Parse).ToArray()
        : AssignedTechnicianId is int id ? [id] : [];

    public void SetTechnicians(IEnumerable<int> ids)
    {
        var technicians = ids.Distinct().ToArray();
        AssignedTechnicianIds = technicians.Length > 1 ? string.Join(",", technicians) : null;
        AssignedTechnicianId = technicians.Length > 0 ? technicians[0] : null;
    }

    public IEnumerable<OperationalBreakdownDamage> GetDamages() => Damages.Count > 0
        ? Damages.OrderBy(d => d.Id)
        : [new() { Category = Category, Element = Element, Target = DamageTarget }];

    public string RepairSummary => (EquipmentUnderRepair, GondolaUnderRepair) switch
    {
        (true, true) => "Equipo y góndola",
        (true, false) => "Equipo",
        (false, true) => "Góndola",
        _ => "—"
    };
}

public sealed class OperationalBreakdownDamage
{
    public int Id { get; set; }
    public int OperationalBreakdownId { get; set; }
    [Required, MaxLength(80)] public string Category { get; set; } = "";
    [Required, MaxLength(200)] public string Element { get; set; } = "";
    [Required, MaxLength(80)] public string Target { get; set; } = "";
}
