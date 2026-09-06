namespace ITServiceDeskApp.Models
{
    public sealed class OperacionesSeguimientoRegistro
    {
        public int Id { get; set; }
        public DateTime FechaOperativa { get; set; }
        public DateTime? FechaEvento { get; set; }
        public string Equipo { get; set; } = string.Empty;
        public string Procedencia { get; set; } = string.Empty;
        public string Ruta { get; set; } = string.Empty;
        public decimal Toneladas { get; set; }
        public int Viajes { get; set; }
        public decimal CombustibleGalones { get; set; }
        public decimal PesoSugerido { get; set; }
        public string Conductor { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Eficiencia { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    public sealed class OperacionesSeguimientoCarga
    {
        public int Id { get; set; }
        public string? SourceFileName { get; set; }
        public DateTime? FechaOperativa { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
