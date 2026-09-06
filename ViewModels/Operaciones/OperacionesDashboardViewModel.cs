namespace ITServiceDeskApp.ViewModels.Operaciones
{
    public sealed class OperacionesDashboardViewModel
    {
        public int DespachosDelDia { get; set; }
        public decimal ToneladasMovilizadas { get; set; }
        public decimal DieselConsumidoGalones { get; set; }
        public decimal DieselConsumidoLitros { get; set; }
        public int CumplimientoPlanPorcentaje { get; set; }
        public decimal MetaDiaria { get; set; } = 500m;
        public decimal ToneladasDescargadasDia { get; set; }
        public decimal ToneladasDescargadasDiaTriton { get; set; }
        public decimal ToneladasDescargadasDiaPavonAsm { get; set; }
        public decimal ToneladasEnRutaActual { get; set; }
        public decimal PrediccionToneladasManana { get; set; }
        public int CumplimientoEstimadoMananaPorcentaje { get; set; }
        public int EquiposFinalizadosDia { get; set; }
        public int EquiposEnRutaDia { get; set; }
        public decimal BrechaMetaToneladas { get; set; }
        public decimal RendimientoToneladasPorGalonMes { get; set; }
        public decimal LitrosPorToneladaMes { get; set; }
        public string EstadoOperativoLabel { get; set; } = "Sin datos";
        public string EstadoOperativoClase { get; set; } = "secondary";
        public List<string> AlertasOperativas { get; set; } = new();
        public decimal MetaMensualToneladas { get; set; }
        public decimal ProyeccionCierreMensualToneladas { get; set; }
        public int CumplimientoProyectadoMensualPorcentaje { get; set; }
        public decimal BrechaProyectadaMensualToneladas { get; set; }
        public string EstadoProyeccionMensualLabel { get; set; } = "Sin datos";
        public string EstadoProyeccionMensualClase { get; set; } = "secondary";
        public DateTime FechaCorteOperativo { get; set; }
        public List<OperacionesDashboardRankingItemViewModel> TopConductores { get; set; } = new();
        public List<OperacionesDashboardRankingItemViewModel> TopEquipos { get; set; } = new();
        public List<OperacionesDashboardRankingItemViewModel> ConductoresMes { get; set; } = new();
        public List<OperacionesDashboardRankingItemViewModel> EquiposMes { get; set; } = new();
        public List<OperacionesDashboardDistribucionItemViewModel> DistribucionProcedencia { get; set; } = new();
        public List<OperacionesDashboardTendenciaItemViewModel> TendenciaDiaria { get; set; } = new();
        public List<OperacionesDashboardComparativoMensualItemViewModel> ComparativoToneladasSitio { get; set; } = new();
        public List<OperacionesDashboardComparativoMensualItemViewModel> ComparativoDieselSitio { get; set; } = new();
        public List<OperacionesDashboardDesviacionItemViewModel> DesviacionesRendimiento { get; set; } = new();
        public List<OperacionesDashboardCumplimientoTurnoViewModel> CumplimientoPorTurno { get; set; } = new();
        public bool IsFromUpload { get; set; }
        public string? SourceFileName { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
    }

    public sealed class OperacionesDashboardRankingItemViewModel
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal Toneladas { get; set; }
    }

    public sealed class OperacionesDashboardDistribucionItemViewModel
    {
        public string Categoria { get; set; } = string.Empty;
        public decimal Toneladas { get; set; }
    }

    public sealed class OperacionesDashboardTendenciaItemViewModel
    {
        public DateTime Fecha { get; set; }
        public decimal Toneladas { get; set; }
    }

    public sealed class OperacionesDashboardComparativoMensualItemViewModel
    {
        public string Mes { get; set; } = string.Empty;
        public decimal Triton { get; set; }
        public decimal PavonAsm { get; set; }
    }

    public sealed class OperacionesDashboardDesviacionItemViewModel
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal Toneladas { get; set; }
        public decimal Galones { get; set; }
        public decimal ToneladasPorGalon { get; set; }
        public decimal VariacionVsPromedioPorcentaje { get; set; }
        public string Estado { get; set; } = string.Empty;
    }

    public sealed class OperacionesDashboardCumplimientoTurnoViewModel
    {
        public string Turno { get; set; } = string.Empty;
        public int Equipos { get; set; }
        public decimal Toneladas { get; set; }
        public decimal MetaTurno { get; set; }
        public int CumplimientoPorcentaje { get; set; }
    }
}
