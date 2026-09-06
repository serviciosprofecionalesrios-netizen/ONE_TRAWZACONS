using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels.Operaciones
{
    public sealed class SeguimientoDieselViewModel
    {
        public List<SeguimientoDieselRowViewModel> Rows { get; set; } = new();
        public decimal TotalGalones { get; set; }
        public decimal TotalLitrosEquivalentes { get; set; }
        public decimal DistanciaPromedioKm { get; set; }
        public decimal ObjetivoKmPorGalon { get; set; }
        public decimal ObjetivoGalonesPorEvento { get; set; }
        public decimal ConsumoPromedioGalonesPorEvento { get; set; }
        public decimal RendimientoPromedioKmPorGalon { get; set; }
        public decimal RendimientoPromedioTonPorGalon { get; set; }
        public decimal IntensidadLitrosPorTonelada { get; set; }
        public decimal CostoReferenciaGalonUsd { get; set; }
        public decimal CostoEstimadoTotalUsd { get; set; }
        public int EquiposSobreconsumo { get; set; }
        public decimal PorcentajeEquiposSobreconsumo { get; set; }
        public int EquiposEstadoOptimo { get; set; }
        public int EquiposEstadoVigilar { get; set; }
        public int EquiposEstadoCritico { get; set; }
        public DateTime? FechaOperativa { get; set; }
        public bool IsFromUpload { get; set; }
        public string? SourceFileName { get; set; }
        public List<SeguimientoDieselAlertaViewModel> Alertas { get; set; } = new();
        public List<SeguimientoDieselRutaResumenViewModel> ResumenPorRuta { get; set; } = new();
        public List<SeguimientoDieselCargaAtipicaViewModel> CargasAtipicas { get; set; } = new();
        public List<SeguimientoDieselTrendPointViewModel> TendenciaSemanal { get; set; } = new();
        public List<SeguimientoDieselTrendPointViewModel> TendenciaMensual { get; set; } = new();
        public List<SeguimientoDieselMantenimientoViewModel> MantenimientoRegistros { get; set; } = new();
        public string MantenimientoFuente { get; set; } = "Data/Operaciones/DieselMantenimientoLog.json";
    }

    public sealed class SeguimientoDieselRowViewModel
    {
        public string Unidad { get; set; } = string.Empty;
        public decimal GalonesDespachados { get; set; }
        public decimal GalonesExtra { get; set; }
        public decimal KilometrosExtra { get; set; }
        public decimal GalonesObjetivoPorEvento { get; set; }
        public decimal GalonesPromedioPorEvento { get; set; }
        public decimal RendimientoEstimadoKmPorGalon { get; set; }
        public decimal VariacionVsMetaConsumoPorcentaje { get; set; }
        public decimal LitrosEquivalentes { get; set; }
        public decimal ToneladasMovilizadas { get; set; }
        public decimal ToneladasPorGalon { get; set; }
        public decimal LitrosPorTonelada { get; set; }
        public decimal VariacionVsPromedioPorcentaje { get; set; }
        public int EventosOperativos { get; set; }
        public int EventosNocturnos { get; set; }
        public int EventosAtipicos { get; set; }
        public string RutaPrincipal { get; set; } = "Sin ruta";
        public string EstadoConsumo { get; set; } = "Sin datos";
        public string Estado { get; set; } = string.Empty;
    }

    public sealed class SeguimientoDieselAlertaViewModel
    {
        public string Nivel { get; set; } = "info";
        public string Unidad { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
    }

    public sealed class SeguimientoDieselRutaResumenViewModel
    {
        public string Ruta { get; set; } = string.Empty;
        public decimal Galones { get; set; }
        public decimal Toneladas { get; set; }
        public decimal ToneladasPorGalon { get; set; }
        public decimal LitrosPorTonelada { get; set; }
    }

    public sealed class SeguimientoDieselCargaAtipicaViewModel
    {
        public string Unidad { get; set; } = string.Empty;
        public string Ruta { get; set; } = string.Empty;
        public DateTime? FechaEvento { get; set; }
        public decimal Galones { get; set; }
        public decimal Toneladas { get; set; }
        public string Motivo { get; set; } = string.Empty;
    }

    public sealed class SeguimientoDieselTrendPointViewModel
    {
        public string Etiqueta { get; set; } = string.Empty;
        public decimal Galones { get; set; }
        public decimal Toneladas { get; set; }
        public decimal LitrosPorTonelada { get; set; }
        public decimal MediaMovil { get; set; }
    }

    public sealed class SeguimientoDieselMantenimientoViewModel
    {
        public DateTime Fecha { get; set; }
        public string Unidad { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Observacion { get; set; } = string.Empty;
        public decimal GalonesUltimos30Dias { get; set; }
    }
}
