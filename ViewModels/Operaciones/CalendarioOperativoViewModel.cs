using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels.Operaciones
{
    public sealed class CalendarioOperativoViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int PreviousYear { get; set; }
        public int PreviousMonth { get; set; }
        public int NextYear { get; set; }
        public int NextMonth { get; set; }
        public string CurrentMonthLabel { get; set; } = string.Empty;

        public decimal MetaDiaria { get; set; } = 500m;
        public decimal ToneladasMes { get; set; }
        public decimal PromedioDiario { get; set; }
        public int DiasConDescarga { get; set; }
        public int DiasMetaCumplida { get; set; }
        public int DiasAlerta { get; set; }
        public int DiasCriticos { get; set; }
        public int EquiposDescargadosMes { get; set; }
        public int TotalEventosFiltrados { get; set; }
        public int DiasConConflictos { get; set; }
        public int TotalConflictos { get; set; }

        public bool IsFromUpload { get; set; }
        public string? SourceFileName { get; set; }
        public DateTime? FechaUltimaData { get; set; }

        public string SelectedSitio { get; set; } = "TODOS";
        public string SelectedTurno { get; set; } = "TODOS";
        public string SelectedEstado { get; set; } = "TODOS";
        public string SearchEquipo { get; set; } = string.Empty;
        public string SearchConductor { get; set; } = string.Empty;
        public DateTime? FechaDesdeFiltro { get; set; }
        public DateTime? FechaHastaFiltro { get; set; }
        public bool HasDateRangeFilter { get; set; }
        public bool SoloConflictos { get; set; }
        public List<string> SitiosDisponibles { get; set; } = new();
        public List<string> TurnosDisponibles { get; set; } = new() { "TODOS", "DIA", "NOCHE" };
        public List<string> EstadosDisponibles { get; set; } = new() { "TODOS", "FINALIZADO", "EN RUTA", "SIN ESTADO" };

        public List<CalendarioOperativoDayViewModel> Days { get; set; } = new();
        public List<CalendarioOperativoConflictoViewModel> Conflictos { get; set; } = new();
    }

    public sealed class CalendarioOperativoDayViewModel
    {
        public DateTime Date { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }

        public decimal ToneladasDescargadas { get; set; }
        public decimal ToneladasTriton { get; set; }
        public decimal ToneladasPavonAsm { get; set; }
        public decimal ToneladasOtros { get; set; }
        public int EquiposDescargados { get; set; }
        public string SemaforoEstado { get; set; } = "none";
        public string SemaforoIcon { get; set; } = "bi-dash-circle";
        public string SemaforoLabel { get; set; } = "Sin descarga";
        public bool HasConflicto { get; set; }
        public int TotalEventos { get; set; }
        public List<CalendarioOperativoEventoViewModel> Eventos { get; set; } = new();
    }

    public sealed class CalendarioOperativoEventoViewModel
    {
        public string HoraLabel { get; set; } = "--:--";
        public string Equipo { get; set; } = string.Empty;
        public string Conductor { get; set; } = string.Empty;
        public string Sitio { get; set; } = string.Empty;
        public string Turno { get; set; } = "DIA";
        public string Estado { get; set; } = "SIN ESTADO";
        public string SemaforoEstado { get; set; } = "danger";
        public string SemaforoIcon { get; set; } = "bi-exclamation-circle";
        public string SemaforoLabel { get; set; } = "Sin estado";
    }

    public sealed class CalendarioOperativoConflictoViewModel
    {
        public DateTime Fecha { get; set; }
        public string HoraSlot { get; set; } = "--:00";
        public string Tipo { get; set; } = string.Empty;
        public string Clave { get; set; } = string.Empty;
        public int Eventos { get; set; }
    }
}
