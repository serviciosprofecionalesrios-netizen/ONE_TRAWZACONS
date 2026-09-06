using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.ViewModels.Operaciones
{
    public sealed class SeguimientoToneladasViewModel
    {
        public List<SeguimientoToneladasRowViewModel> Rows { get; set; } = new();
        public List<SeguimientoToneladasEstadoResumenViewModel> EstadoResumen { get; set; } = new();
        public decimal TotalToneladas { get; set; }
        public int TotalViajes { get; set; }
        public decimal PromedioPorViaje { get; set; }
        public int TotalEquipos { get; set; }
        public decimal ToneladasAcumuladasMes { get; set; }
        public decimal ToneladasAlDia { get; set; }
        public decimal TotalCombustibleLitros { get; set; }
        public int EquiposDescargados { get; set; }
        public int EquiposEnRuta { get; set; }
        public decimal ToneladasEnRuta { get; set; }
        public DateTime? FechaOperativa { get; set; }
        public int MetaAnio { get; set; }
        public int MetaMes { get; set; }
        public decimal MetaMensualTriton { get; set; }
        public decimal MetaMensualPavonAsm { get; set; }
        public decimal MetaMensualTotal { get; set; }
        public decimal MetaDiariaObjetivo { get; set; } = 750m;
        public decimal MetaDiariaTritonObjetivo { get; set; } = 500m;
        public decimal MetaDiariaPavonAsmObjetivo { get; set; } = 250m;
        public bool MetaMensualPersonalizada { get; set; }
        public bool IsFromUpload { get; set; }
        public string? SourceFileName { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public sealed class SeguimientoToneladasRowViewModel
    {
        public string Equipo { get; set; } = string.Empty;
        public string Procedencia { get; set; } = string.Empty;
        public string Ruta { get; set; } = "Sin ruta";
        public decimal Toneladas { get; set; }
        public int Viajes { get; set; }
        public decimal CombustibleLitros { get; set; }
        public DateTime? FechaEvento { get; set; }
        public string Conductor { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Eficiencia { get; set; } = string.Empty;
        public decimal PesoSugerido { get; set; }
    }

    public sealed class SeguimientoToneladasEstadoResumenViewModel
    {
        public string Estado { get; set; } = string.Empty;
        public int Equipos { get; set; }
        public decimal Toneladas { get; set; }
        public int Viajes { get; set; }
        public string EquiposDetalle { get; set; } = string.Empty;
    }
}
