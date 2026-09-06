using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels.Operaciones
{
    public sealed class OperacionesReportesViewModel
    {
        public OperacionesReportesFiltrosViewModel Filtros { get; set; } = new();
        public OperacionesReportesResumenEjecutivoViewModel ResumenEjecutivo { get; set; } = new();
        public List<OperacionesReportesComparativoViewModel> Comparativos { get; set; } = new();
        public List<string> Hallazgos { get; set; } = new();
        public OperacionesReportesCalidadDatosViewModel CalidadDatos { get; set; } = new();
        public List<OperacionesReportesCategoriaViewModel> Categorias { get; set; } = new();
        public List<OperacionesReportesAuditoriaViewModel> HistorialGeneracion { get; set; } = new();
        public List<OperacionesReportesProgramacionViewModel> Programaciones { get; set; } = new();
        public List<string> SitiosDisponibles { get; set; } = new();
        public List<string> TurnosDisponibles { get; set; } = new() { "TODOS", "DIA", "NOCHE" };
        public List<string> EstadosDisponibles { get; set; } = new() { "TODOS", "FINALIZADO", "EN RUTA" };
        public bool IsFromUpload { get; set; }
        public string? SourceFileName { get; set; }
        public DateTime? FechaCorte { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public sealed class OperacionesReportesFiltrosViewModel
    {
        public string FechaDesde { get; set; } = string.Empty;
        public string FechaHasta { get; set; } = string.Empty;
        public string Sitio { get; set; } = "TODOS";
        public string Turno { get; set; } = "TODOS";
        public string Conductor { get; set; } = string.Empty;
        public string Equipo { get; set; } = string.Empty;
        public string Estado { get; set; } = "TODOS";
    }

    public sealed class OperacionesReportesResumenEjecutivoViewModel
    {
        public decimal Toneladas { get; set; }
        public decimal DieselGalones { get; set; }
        public int CumplimientoPorcentaje { get; set; }
        public decimal VariacionVsPeriodoAnteriorPorcentaje { get; set; }
        public string EstadoOperativo { get; set; } = "Sin datos";
        public string EstadoOperativoClase { get; set; } = "secondary";
        public int Registros { get; set; }
        public int EquiposUnicos { get; set; }
        public int ConductoresUnicos { get; set; }
    }

    public sealed class OperacionesReportesComparativoViewModel
    {
        public string Periodo { get; set; } = string.Empty;
        public decimal ActualToneladas { get; set; }
        public decimal AnteriorToneladas { get; set; }
        public decimal ActualDieselGal { get; set; }
        public decimal AnteriorDieselGal { get; set; }
        public decimal DeltaToneladas { get; set; }
        public decimal DeltaPorcentaje { get; set; }
    }

    public sealed class OperacionesReportesCalidadDatosViewModel
    {
        public int RegistrosTotales { get; set; }
        public int RegistrosIncompletos { get; set; }
        public decimal PorcentajeIncompleto { get; set; }
        public int SinFecha { get; set; }
        public int SinConductor { get; set; }
        public int SinEquipo { get; set; }
        public int SinEstado { get; set; }
        public int SinRuta { get; set; }
    }

    public sealed class OperacionesReportesCategoriaViewModel
    {
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public List<OperacionesReportesItemViewModel> Reportes { get; set; } = new();
    }

    public sealed class OperacionesReportesItemViewModel
    {
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public List<string> Highlights { get; set; } = new();
        public string PdfDownloadUrl { get; set; } = string.Empty;
        public string PdfPreviewUrl { get; set; } = string.Empty;
        public string ExcelDownloadUrl { get; set; } = string.Empty;
        public string CsvDownloadUrl { get; set; } = string.Empty;
    }

    public sealed class OperacionesReportesAuditoriaViewModel
    {
        public DateTime FechaGeneracion { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string Reporte { get; set; } = string.Empty;
        public string Formato { get; set; } = string.Empty;
        public int Registros { get; set; }
        public string FiltrosAplicados { get; set; } = string.Empty;
    }

    public sealed class OperacionesReportesProgramacionViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Reporte { get; set; } = string.Empty;
        public string Frecuencia { get; set; } = string.Empty;
        public string Hora { get; set; } = string.Empty;
        public string DiaSemana { get; set; } = string.Empty;
        public string Destinatarios { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? ProximaEjecucion { get; set; }
        public DateTime? UltimaEjecucion { get; set; }
    }
}
