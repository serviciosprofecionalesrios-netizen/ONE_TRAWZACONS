namespace ITServiceDeskApp.Services.Interfaces
{
    public interface IOperacionesIngresoEquipoGondolaTraceStore
    {
        OperacionesIngresoEquipoGondolaTraceResult ResolveOrCreate(string tipo, string asunto);
        IReadOnlyList<OperacionesIngresoEquipoGondolaTraceHistoryItem> GetRecent(int take);
    }

    public sealed class OperacionesIngresoEquipoGondolaTraceResult
    {
        public bool Success { get; init; }
        public string TareaPadre { get; init; } = string.Empty;
        public bool Created { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public sealed class OperacionesIngresoEquipoGondolaTraceHistoryItem
    {
        public string Solicitante { get; init; } = string.Empty;
        public string TareaPadre { get; init; } = string.Empty;
        public string TipoOrigen { get; init; } = string.Empty;
        public string UltimoTipo { get; init; } = string.Empty;
        public string UltimoAsunto { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
