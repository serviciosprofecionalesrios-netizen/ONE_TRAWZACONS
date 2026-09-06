namespace ITServiceDeskApp.Models
{
    public sealed class OperacionesIngresoEquipoGondolaSolicitud
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Asunto { get; set; } = string.Empty;
        public string SolicitanteNombre { get; set; } = string.Empty;
        public string TareaPadre { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Prioridad { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
