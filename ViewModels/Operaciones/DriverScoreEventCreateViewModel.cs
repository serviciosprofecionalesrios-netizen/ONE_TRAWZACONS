using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.ViewModels.Operaciones;

public sealed class DriverScoreEventCreateViewModel
{
    [Required] public DateTime EventAt { get; set; } = DateTime.Now;
    [Required, Display(Name = "Conductor")] public string Driver { get; set; } = string.Empty;
    [Required, Display(Name = "Unidad")] public string Vehicle { get; set; } = string.Empty;
    [Required, Display(Name = "Tipo de infracción")] public string EventType { get; set; } = string.Empty;
    [Display(Name = "Grupo")] public string? Group { get; set; }
    [Display(Name = "Ubicación (latitud, longitud)")] public string? Location { get; set; }
    [Display(Name = "Descripción u observación")] public string? Observation { get; set; }
    [Display(Name = "Gestionado a tiempo")] public bool TimelyManaged { get; set; }
    [Display(Name = "Se realizó coaching")] public bool CoachingCompleted { get; set; }
    public IReadOnlyList<string> Drivers { get; set; } = [];
    public IReadOnlyList<string> Vehicles { get; set; } = [];
    public IReadOnlyList<string> EventTypes { get; set; } = [];
}
