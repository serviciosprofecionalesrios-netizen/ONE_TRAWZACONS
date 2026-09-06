namespace ITServiceDeskApp.ViewModels.Operaciones
{
    public sealed class ControlDocumentosViewModel
    {
        public string FuenteArchivo { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public IReadOnlyList<string> EncabezadosCapacitacion { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ControlDocumentosRowViewModel> Filas { get; set; } = Array.Empty<ControlDocumentosRowViewModel>();
        public IReadOnlyList<ControlDocumentosCumplimientoCapacitacionViewModel> CumplimientoCapacitaciones { get; set; } = Array.Empty<ControlDocumentosCumplimientoCapacitacionViewModel>();
        public IReadOnlyList<ControlDocumentosHistoryEntryViewModel> HistorialCambios { get; set; } = Array.Empty<ControlDocumentosHistoryEntryViewModel>();
        public IReadOnlyList<ControlDocumentosTrendPointViewModel> TendenciaSemanal { get; set; } = Array.Empty<ControlDocumentosTrendPointViewModel>();
        public IReadOnlyList<ControlDocumentosTrendPointViewModel> TendenciaMensual { get; set; } = Array.Empty<ControlDocumentosTrendPointViewModel>();
        public IReadOnlyList<ControlDocumentosEquipoGondolaRowViewModel> EquiposGondolas { get; set; } = Array.Empty<ControlDocumentosEquipoGondolaRowViewModel>();
        public ControlDocumentosAlertaResumenViewModel AlertasResumen { get; set; } = new();
        public DateTime FechaGeneracion { get; set; } = DateTime.Now;

        public int TotalConductores => Filas.Count;
        public int TotalCapacitaciones => EncabezadosCapacitacion.Count;
        public int TotalRegistros => TotalConductores * TotalCapacitaciones;
        public int TotalEquiposGondolas => EquiposGondolas.Count;
    }

    public sealed class ControlDocumentosRowViewModel
    {
        public string Conductor { get; set; } = string.Empty;
        public IReadOnlyList<string> FechasCapacitacion { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> FechasCapacitacionIso { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ControlDocumentosCapacitacionEstadoViewModel> Capacitaciones { get; set; } = Array.Empty<ControlDocumentosCapacitacionEstadoViewModel>();
    }

    public sealed class ControlDocumentosCapacitacionEstadoViewModel
    {
        public string NombreCapacitacion { get; set; } = string.Empty;
        public string Fecha { get; set; } = "-";
        public string FechaIso { get; set; } = string.Empty;
        public string FechaVencimiento { get; set; } = "-";
        public int ValidezDias { get; set; }
        public int? DiasRestantes { get; set; }
        public string EstadoSemaforo { get; set; } = "rojo";
        public string EstadoFiltro { get; set; } = "sinfecha";
        public string EtiquetaSemaforo { get; set; } = "Sin fecha";
        public string MensajeAlerta { get; set; } = "No hay fecha registrada.";
        public bool TieneFecha => !string.IsNullOrWhiteSpace(Fecha) && Fecha != "-";
    }

    public sealed class ControlDocumentosEquipoGondolaRowViewModel
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Asunto { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Prioridad { get; set; } = string.Empty;
        public string TareaPadre { get; set; } = string.Empty;
        public string Empresa { get; set; } = string.Empty;
        public string CodigoCabezal { get; set; } = string.Empty;
        public string PlacaCabezal { get; set; } = string.Empty;
        public string Tenencia { get; set; } = string.Empty;
        public string Propietario { get; set; } = string.Empty;
        public string PlacaGondola { get; set; } = string.Empty;
        public string VencimientoEmisionGases { get; set; } = string.Empty;
        public string VencimientoInspeccionMecanica { get; set; } = string.Empty;
        public string VencimientoSeguroVehicular { get; set; } = string.Empty;
        public string FechaInicio { get; set; } = string.Empty;
        public string FechaFin { get; set; } = string.Empty;
        public string PorcentajeRealizado { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class ControlDocumentosUpdateInput
    {
        public string Conductor { get; set; } = string.Empty;
        public List<string> Encabezados { get; set; } = new();
        public List<string> Fechas { get; set; } = new();
        public string SupervisorNombre { get; set; } = string.Empty;
        public bool ConfirmacionSupervisor { get; set; }
    }

    public sealed class ControlDocumentosBulkUpdateInput
    {
        public string Capacitacion { get; set; } = string.Empty;
        public string Fecha { get; set; } = string.Empty;
        public List<string> Conductores { get; set; } = new();
        public string SupervisorNombre { get; set; } = string.Empty;
        public bool ConfirmacionSupervisor { get; set; }
    }

    public sealed class ControlDocumentosNuevoRegistroInput
    {
        public string TipoRegistro { get; set; } = string.Empty;
        public string PersonaNombre { get; set; } = string.Empty;
        public List<string> PersonaEncabezados { get; set; } = new();
        public List<string> PersonaFechas { get; set; } = new();
        public string EquipoTipoIngreso { get; set; } = string.Empty;
        public string EquipoAsunto { get; set; } = string.Empty;
        public string EquipoEmpresa { get; set; } = string.Empty;
        public string EquipoCodigoCabezal { get; set; } = string.Empty;
        public string EquipoPlacaCabezal { get; set; } = string.Empty;
        public string EquipoPlacaGondola { get; set; } = string.Empty;
        public string EquipoTenencia { get; set; } = string.Empty;
        public string EquipoPropietario { get; set; } = string.Empty;
        public string EquipoVencimientoEmisionGases { get; set; } = string.Empty;
        public string EquipoVencimientoInspeccionMecanica { get; set; } = string.Empty;
        public string EquipoVencimientoSeguroVehicular { get; set; } = string.Empty;
    }

    public sealed class ControlDocumentosCumplimientoCapacitacionViewModel
    {
        public string Capacitacion { get; set; } = string.Empty;
        public int TotalConductores { get; set; }
        public int Vigentes { get; set; }
        public int PorVencer { get; set; }
        public int Vencidas { get; set; }
        public int SinFecha { get; set; }
        public decimal CumplimientoPorcentaje { get; set; }
        public string EstadoVisual { get; set; } = "rojo";
    }

    public sealed class ControlDocumentosHistoryEntryViewModel
    {
        public DateTime FechaCambio { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string Supervisor { get; set; } = string.Empty;
        public string Conductor { get; set; } = string.Empty;
        public string Capacitacion { get; set; } = string.Empty;
        public string ValorAnterior { get; set; } = "-";
        public string ValorNuevo { get; set; } = "-";
        public bool RequiereAprobacionCritica { get; set; }
    }

    public sealed class ControlDocumentosQrViewModel
    {
        public string Conductor { get; set; } = string.Empty;
        public string ConductorKey { get; set; } = string.Empty;
        public string QrImageUrl { get; set; } = string.Empty;
        public string RegistroUrl { get; set; } = string.Empty;
        public string QrPdfUrl { get; set; } = string.Empty;
        public DateTime FechaGeneracion { get; set; } = DateTime.Now;
        public IReadOnlyList<ControlDocumentosCapacitacionEstadoViewModel> Capacitaciones { get; set; } = Array.Empty<ControlDocumentosCapacitacionEstadoViewModel>();
        public IReadOnlyList<ControlDocumentosQrScanEntryViewModel> EscaneosRecientes { get; set; } = Array.Empty<ControlDocumentosQrScanEntryViewModel>();
    }

    public sealed class ControlDocumentosTrendPointViewModel
    {
        public string Etiqueta { get; set; } = string.Empty;
        public int TotalCambios { get; set; }
        public int CambiosCriticos { get; set; }
    }

    public sealed class ControlDocumentosAlertaResumenViewModel
    {
        public DateTime? UltimaEjecucion { get; set; }
        public int Evaluadas { get; set; }
        public int EnviadasEmail { get; set; }
        public int EnviadasWhatsApp { get; set; }
        public int Pendientes { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }

    public sealed class ControlDocumentosQrScanEntryViewModel
    {
        public DateTime FechaEscaneo { get; set; }
        public string Usuario { get; set; } = "Anonimo";
        public string IpAddress { get; set; } = "-";
        public string UserAgent { get; set; } = "-";
    }
}
