namespace ITServiceDeskApp.ViewModels.Operaciones;
public sealed class DocumentMasterFormViewModel
{
    public int? MasterId { get; set; }
    public string DisplayId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Tenure { get; set; } = "";
    public string Owner { get; set; } = "";
    public int? RequestId { get; set; }
    public string Type { get; set; } = "";
    public Dictionary<string, string[]> Payload { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
