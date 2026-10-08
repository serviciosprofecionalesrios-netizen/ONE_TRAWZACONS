namespace ITServiceDeskApp.Services;
public static class DriverScoreCasePolicy
{
    public static bool CanSave(string status, string? responsible, bool hasEvidence) =>
        status == "Pendiente" || status == "En seguimiento" && !string.IsNullOrWhiteSpace(responsible) || status == "Cerrado" && !string.IsNullOrWhiteSpace(responsible) && hasEvidence;
}
