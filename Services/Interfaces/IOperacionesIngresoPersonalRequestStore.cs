namespace ITServiceDeskApp.Services.Interfaces
{
    public interface IOperacionesIngresoPersonalRequestStore
    {
        OperacionesIngresoPersonalRequestSaveResult Save(OperacionesIngresoPersonalRequestSaveInput input);
        IReadOnlyList<OperacionesIngresoPersonalRequestListItem> GetRecent(int take);
        OperacionesIngresoPersonalRequestDetail? GetById(int id);
        bool Delete(int id);
    }

    public sealed class OperacionesIngresoPersonalRequestSaveInput
    {
        public int? Id { get; init; }
        public string Tipo { get; init; } = string.Empty;
        public string Asunto { get; init; } = string.Empty;
        public string SolicitanteNombre { get; init; } = string.Empty;
        public string TareaPadre { get; init; } = string.Empty;
        public string Estado { get; init; } = string.Empty;
        public string Prioridad { get; init; } = string.Empty;
        public Dictionary<string, string[]> Payload { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class OperacionesIngresoPersonalRequestSaveResult
    {
        public bool Success { get; init; }
        public bool Created { get; init; }
        public int Id { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public sealed class OperacionesIngresoPersonalRequestListItem
    {
        public int Id { get; init; }
        public string Tipo { get; init; } = string.Empty;
        public string Asunto { get; init; } = string.Empty;
        public string SolicitanteNombre { get; init; } = string.Empty;
        public string TareaPadre { get; init; } = string.Empty;
        public string Estado { get; init; } = string.Empty;
        public string Prioridad { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    public sealed class OperacionesIngresoPersonalRequestDetail
    {
        public int Id { get; init; }
        public string Tipo { get; init; } = string.Empty;
        public string Asunto { get; init; } = string.Empty;
        public string SolicitanteNombre { get; init; } = string.Empty;
        public string TareaPadre { get; init; } = string.Empty;
        public string Estado { get; init; } = string.Empty;
        public string Prioridad { get; init; } = string.Empty;
        public Dictionary<string, string[]> Payload { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
