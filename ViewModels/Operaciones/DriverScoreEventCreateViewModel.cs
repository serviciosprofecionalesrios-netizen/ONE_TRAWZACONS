using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.ViewModels.Operaciones;

public sealed class DriverScoreEventCreateViewModel
{
    [Required, Display(Name = "Fecha y hora del evento")] public DateTime EventAt { get; set; } = DateTime.Now;
    public IFormFile? CoachingEvidence { get; set; }
    [Required, Display(Name = "Sitio")] public string Site { get; set; } = string.Empty;
    public string Status { get; set; } = "Pendiente";
    [Required(ErrorMessage = "Selecciona un responsable.")] public string? Responsible { get; set; }
    [MaxLength(1000)] public string? CaseNotes { get; set; }
    public IFormFile? ClosureEvidence { get; set; }
    [Required, Display(Name = "Conductor")] public string Driver { get; set; } = string.Empty;
    [Required, Display(Name = "Unidad")] public string Vehicle { get; set; } = string.Empty;
    [Required, Display(Name = "Tipo de infracción")] public string EventType { get; set; } = string.Empty;
    [Required, MaxLength(120), Display(Name = "Grupo")] public string? Group { get; set; }
    [Required(ErrorMessage = "La ubicación es obligatoria. No se puede guardar sin ella."), MaxLength(80), Display(Name = "Ubicación (latitud, longitud)")] public string? Location { get; set; }
    [Required, MaxLength(1000), Display(Name = "Descripción u observación")] public string? Observation { get; set; }

    [Display(Name = "Se realizó coaching")] public bool CoachingCompleted { get; set; }
    public IReadOnlyList<string> Drivers { get; set; } = [];
    public IReadOnlyList<string> Vehicles { get; set; } = [];
    public IReadOnlyList<string> EventTypes { get; set; } = [];
}
