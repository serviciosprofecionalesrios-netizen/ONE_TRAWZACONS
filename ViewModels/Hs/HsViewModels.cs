using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ITServiceDeskApp.Models;
using Microsoft.AspNetCore.Http;

namespace ITServiceDeskApp.ViewModels.Hs
{
    public sealed class HsDashboardViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int OpenIncidents { get; set; }
        public int CriticalIncidents { get; set; }
        public int LostTimeIncidents { get; set; }
        public int OpenActions { get; set; }
        public int OverdueActions { get; set; }
        public int Inspections { get; set; }
        public decimal AverageInspectionScore { get; set; }
        public int Trainings { get; set; }
        public int TrainedPeople { get; set; }
        public int ActivePermits { get; set; }
        public int PendingPermitApprovals { get; set; }
        public int OverduePermitApprovals { get; set; }
        public int HighRiskIncidents { get; set; }
        public int PendingInvestigations { get; set; }
        public int PendingApprovals { get; set; }
        public int ExpiredTrainings { get; set; }
        public int DocumentsDueForReview { get; set; }
        public int DaysWithoutLostTimeIncident { get; set; }
        public decimal Trir { get; set; }
        public decimal Ltifr { get; set; }
        public List<HsAlertViewModel> Alerts { get; set; } = new();
        public List<HsIncident> RecentIncidents { get; set; } = new();
        public List<HsCorrectiveAction> DueActions { get; set; } = new();
        public List<HsInspection> LowScoreInspections { get; set; } = new();
        public List<HsSiteSummaryViewModel> SiteSummaries { get; set; } = new();
        public List<HsCalendarItemViewModel> CalendarItems { get; set; } = new();
        public List<HsTruckInspectionSummaryViewModel> TruckInspectionSummaries { get; set; } = new();
    }

    public sealed class HsAlertViewModel
    {
        public string Tone { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
    }

    public sealed class HsSiteSummaryViewModel
    {
        public string Site { get; set; } = string.Empty;
        public int Incidents { get; set; }
        public int OpenActions { get; set; }
        public decimal AverageScore { get; set; }
    }

    public sealed class HsTruckInspectionSummaryViewModel
    {
        public string EquipmentCode { get; set; } = string.Empty;
        public int Total { get; set; }
        public DateTime LastInspectionAt { get; set; }
        public decimal LastScorePercent { get; set; }
        public int OpenFindings { get; set; }
    }

    public sealed class HsCalendarItemViewModel
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Tone { get; set; } = "secondary";
    }

    public sealed class HsIncidentEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        [Display(Name = "Fecha y hora")]
        public DateTime OccurredAt { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(100)]
        [Display(Name = "Sitio")]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Area")]
        public string Area { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Reportado por")]
        public string ReportedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(60)]
        [Display(Name = "Tipo")]
        public string IncidentType { get; set; } = "Condicion insegura";

        [Required]
        [MaxLength(40)]
        [Display(Name = "Severidad")]
        public string Severity { get; set; } = "Media";

        [Required]
        [MaxLength(40)]
        [Display(Name = "Estado")]
        public string Status { get; set; } = "Abierto";

        [Required]
        [MaxLength(2000)]
        [Display(Name = "Descripcion")]
        public string Description { get; set; } = string.Empty;

        [MaxLength(1500)]
        [Display(Name = "Accion inmediata")]
        public string? ImmediateAction { get; set; }

        [MaxLength(1500)]
        [Display(Name = "Causa raiz")]
        public string? RootCause { get; set; }

        [MaxLength(1200)]
        [Display(Name = "Causa inmediata")]
        public string? ImmediateCause { get; set; }

        [MaxLength(1200)]
        [Display(Name = "Analisis 5 porques")]
        public string? FiveWhyAnalysis { get; set; }

        [MaxLength(800)]
        [Display(Name = "Testigos")]
        public string? Witnesses { get; set; }

        [MaxLength(120)]
        [Display(Name = "Investigador")]
        public string? Investigator { get; set; }

        [Display(Name = "Fecha compromiso investigacion")]
        public DateTime? InvestigationDueDate { get; set; }

        [Range(1, 5)]
        [Display(Name = "Probabilidad")]
        public int RiskProbability { get; set; } = 3;

        [Range(1, 5)]
        [Display(Name = "Severidad del riesgo")]
        public int RiskSeverity { get; set; } = 3;

        [Range(1, 5)]
        [Display(Name = "Probabilidad residual")]
        public int ResidualRiskProbability { get; set; } = 2;

        [Range(1, 5)]
        [Display(Name = "Severidad residual")]
        public int ResidualRiskSeverity { get; set; } = 2;

        [MaxLength(1500)]
        [Display(Name = "Controles actuales")]
        public string? CurrentControls { get; set; }

        [MaxLength(1500)]
        [Display(Name = "Controles recomendados")]
        public string? RecommendedControls { get; set; }

        [Display(Name = "Tiempo perdido")]
        public bool LostTime { get; set; }

        [Range(0, 365)]
        [Display(Name = "Dias perdidos")]
        public int LostDays { get; set; }

        [Display(Name = "Evidencia")]
        public IFormFile? EvidenceFile { get; set; }
    }

    public sealed class HsInspectionEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        [Display(Name = "Fecha")]
        public DateTime InspectionDate { get; set; } = DateTime.Today;

        [Required]
        [MaxLength(100)]
        [Display(Name = "Sitio")]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Area")]
        public string Area { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Inspector")]
        public string Inspector { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        [Display(Name = "Categoria")]
        public string Category { get; set; } = "Inspeccion general";

        [Range(typeof(decimal), "0", "100")]
        [Display(Name = "Cumplimiento %")]
        public decimal ScorePercent { get; set; } = 100m;

        [Range(0, 1000)]
        [Display(Name = "Hallazgos")]
        public int FindingsCount { get; set; }

        [Range(0, 1000)]
        [Display(Name = "Hallazgos criticos")]
        public int CriticalFindingsCount { get; set; }

        [Required]
        [Display(Name = "Estado")]
        public string Status { get; set; } = "Completada";

        [MaxLength(2000)]
        [Display(Name = "Notas")]
        public string? Notes { get; set; }
    }

    public sealed class HsCorrectiveActionEditViewModel
    {
        public int? Id { get; set; }
        public string SourceType { get; set; } = "Manual";
        public int? SourceId { get; set; }

        [Required]
        [MaxLength(180)]
        [Display(Name = "Titulo")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        [Display(Name = "Descripcion")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Responsable")]
        public string Owner { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        [Display(Name = "Prioridad")]
        public string Priority { get; set; } = "Media";

        [Required]
        [MaxLength(40)]
        [Display(Name = "Estado")]
        public string Status { get; set; } = "Abierta";

        [Required]
        [Display(Name = "Fecha compromiso")]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(7);

        [MaxLength(1200)]
        [Display(Name = "Notas de cierre")]
        public string? ClosureNotes { get; set; }

        [MaxLength(1200)]
        [Display(Name = "Notas de aprobacion")]
        public string? ApprovalNotes { get; set; }
    }

    public sealed class HsTrainingEditViewModel
    {
        [Required]
        [Display(Name = "Fecha")]
        public DateTime TrainingDate { get; set; } = DateTime.Today;

        [Required]
        [MaxLength(160)]
        [Display(Name = "Tema")]
        public string Topic { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Instructor")]
        public string Trainer { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Display(Name = "Sitio")]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Audiencia")]
        public string Audience { get; set; } = "Personal operativo";

        [MaxLength(120)]
        [Display(Name = "Persona")]
        public string? EmployeeName { get; set; }

        [MaxLength(60)]
        [Display(Name = "Codigo")]
        public string? EmployeeCode { get; set; }

        [MaxLength(120)]
        [Display(Name = "Puesto")]
        public string? Position { get; set; }

        [Range(0, 10000)]
        [Display(Name = "Participantes")]
        public int AttendeeCount { get; set; }

        [Display(Name = "Capacitacion obligatoria")]
        public bool RequiredTraining { get; set; } = true;

        [Display(Name = "Vence")]
        public DateTime? ExpirationDate { get; set; }

        [MaxLength(40)]
        [Display(Name = "Resultado")]
        public string Result { get; set; } = "Aprobado";

        [Display(Name = "Evidencia")]
        public IFormFile? EvidenceFile { get; set; }

        [MaxLength(1500)]
        [Display(Name = "Notas")]
        public string? Notes { get; set; }
    }

    public sealed class HsWorkPermitEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(80)]
        [Display(Name = "Tipo de permiso")]
        public string PermitType { get; set; } = "Trabajo en caliente";

        [Required]
        [MaxLength(100)]
        [Display(Name = "Sitio")]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Area")]
        public string Area { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Solicitado por")]
        public string RequestedBy { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Inicio")]
        public DateTime StartAt { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Fin")]
        public DateTime EndAt { get; set; } = DateTime.Now.AddHours(8);

        [Required]
        [MaxLength(40)]
        [Display(Name = "Estado")]
        public string Status { get; set; } = "Solicitado";

        [MaxLength(1500)]
        [Display(Name = "Controles requeridos")]
        public string? Controls { get; set; }

        [Display(Name = "AST/JSA o matriz de riesgo")]
        public bool HasRiskAssessment { get; set; }

        [Display(Name = "Area aislada")]
        public bool HasAreaIsolation { get; set; }

        [Display(Name = "EPP completo")]
        public bool HasPpe { get; set; }

        [Display(Name = "Plan de emergencia")]
        public bool HasEmergencyPlan { get; set; }

        [Display(Name = "Aprobacion supervisor")]
        public bool HasSupervisorApproval { get; set; }

        [MaxLength(1200)]
        [Display(Name = "Notas checklist")]
        public string? ChecklistNotes { get; set; }

        [Display(Name = "Evidencias fotograficas")]
        public List<IFormFile> EvidenceFiles { get; set; } = new();

        public string? EvidencePaths { get; set; }
    }

    public sealed class HsDocumentEditViewModel
    {
        [Required]
        [MaxLength(160)]
        [Display(Name = "Titulo")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        [Display(Name = "Tipo")]
        public string DocumentType { get; set; } = "Procedimiento";

        [Required]
        [MaxLength(40)]
        [Display(Name = "Codigo")]
        public string Code { get; set; } = string.Empty;

        [MaxLength(30)]
        [Display(Name = "Version")]
        public string Version { get; set; } = "1.0";

        [MaxLength(100)]
        [Display(Name = "Sitio")]
        public string Site { get; set; } = "General";

        [Display(Name = "Vigente desde")]
        public DateTime EffectiveDate { get; set; } = DateTime.Today;

        [Display(Name = "Revision")]
        public DateTime? ReviewDate { get; set; } = DateTime.Today.AddMonths(12);

        [MaxLength(40)]
        [Display(Name = "Estado")]
        public string Status { get; set; } = "Vigente";

        [Display(Name = "Archivo")]
        public IFormFile? DocumentFile { get; set; }
    }
}
