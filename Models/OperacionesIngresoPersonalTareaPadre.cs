namespace ITServiceDeskApp.Models
{
    public sealed class OperacionesIngresoPersonalTareaPadre
    {
        public int Id { get; set; }
        public string SolicitanteKey { get; set; } = string.Empty;
        public string SolicitanteNombre { get; set; } = string.Empty;
        public string TareaPadre { get; set; } = string.Empty;
        public string TipoOrigen { get; set; } = string.Empty;
        public string UltimoTipo { get; set; } = string.Empty;
        public string UltimoAsunto { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
