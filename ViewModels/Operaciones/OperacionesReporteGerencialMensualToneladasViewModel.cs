using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels.Operaciones
{
    public sealed class OperacionesReporteGerencialMensualToneladasViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime FechaCorte { get; set; }
        public string PeriodoLabel { get; set; } = string.Empty;
        public string FuenteDatos { get; set; } = "Seguimiento de toneladas";
        public int TotalRegistros { get; set; }
        public decimal TotalToneladas { get; set; }
        public decimal TotalDieselGalones { get; set; }
        public decimal MetaDiariaTriton { get; set; }
        public decimal MetaDiariaPavonAsm { get; set; }
        public decimal MetaMensualTriton { get; set; }
        public decimal MetaMensualPavonAsm { get; set; }
        public List<OperacionesReporteSitioResumenViewModel> Sitios { get; set; } = new();
        public List<OperacionesReporteRankingRowViewModel> TopOperadores { get; set; } = new();
        public List<OperacionesReporteRankingRowViewModel> TopEquipos { get; set; } = new();
        public List<OperacionesReporteRankingRowViewModel> BottomConductores { get; set; } = new();
        public List<OperacionesReporteRankingRowViewModel> BottomEquipos { get; set; } = new();
        public List<OperacionesReporteCumplimientoDiaSitioViewModel> CumplimientoPorSitio { get; set; } = new();
        public List<OperacionesReporteDiaCumplidoViewModel> DiasMetaCumplidaDetalle { get; set; } = new();
    }

    public sealed class OperacionesReporteSitioResumenViewModel
    {
        public string Sitio { get; set; } = string.Empty;
        public decimal Toneladas { get; set; }
        public decimal DieselGalones { get; set; }
        public decimal MetaMensual { get; set; }
        public decimal CumplimientoMensualPct { get; set; }
        public decimal MetaDiaria { get; set; }
    }

    public sealed class OperacionesReporteRankingRowViewModel
    {
        public int Posicion { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Sitio { get; set; } = string.Empty;
        public decimal Toneladas { get; set; }
        public int Viajes { get; set; }
    }

    public sealed class OperacionesReporteCumplimientoDiaSitioViewModel
    {
        public string Sitio { get; set; } = string.Empty;
        public decimal MetaDiaria { get; set; }
        public int DiasConOperacion { get; set; }
        public int DiasMetaCumplida { get; set; }
        public decimal CumplimientoDiasPct { get; set; }
        public decimal PromedioToneladasDia { get; set; }
        public decimal MejorDiaToneladas { get; set; }
        public DateTime? FechaMejorDia { get; set; }
    }

    public sealed class OperacionesReporteDiaCumplidoViewModel
    {
        public string Sitio { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public decimal Toneladas { get; set; }
        public decimal MetaDiaria { get; set; }
        public decimal CumplimientoPct { get; set; }
    }
}
