using ITServiceDeskApp.ViewModels.Operaciones;

namespace ITServiceDeskApp.Controllers;

public partial class OperacionesController
{
    private static int? ControlDocumentoValidityYears(string header) => NormalizeHeader(header) switch
    {
        "inducciongeneraldesitio" or "inducciongeneral" or "inducciondesitio" or
        "primerosauxiliosteorico" or "primerosauxiliosteoricos" or "primerosauxiliostecnico" or
        "primerosauxiliospractico" or "primerosauxiliospracticos" => 3,
        "ncs" or "usodeformato" or "usodeformatodetransporte" or "usodeformatosdetransporte" or
        "educacionvial" or "trasladodeminaamina" or "opertrasladominaamina" or
        "operaciondetrasladominaamina" or "gestiondefatiga" or "fatiga" => 2,
        // Personal documents retain their existing rules; the five-year default is for training.
        "cedula" or "inss" or "contratodetrabajo" or "licencia" or "examenmedico" => null,
        _ => 5
    };

    private static bool IsMechanicSupervisorTraining(string header) => NormalizeHeader(header) is
        "ats" or "manejodeequiposlivianos" or "manejodeequipoliviano" or
        "manejodefensivoparaequiposlivianos" or "manejodefensivoequiposlivianos" or
        "manejodefensivoliviano" or "manejodefensivovliviano";

    private static bool HasMechanicSupervisorPosition(IReadOnlyDictionary<string, string> data) =>
        data.Any(field => (NormalizeHeader(field.Key) is "cargo" or "puesto" or "ocupacion") &&
            System.Text.RegularExpressions.Regex.IsMatch(field.Value.Normalize(System.Text.NormalizationForm.FormD)
                .Replace("\u0301", ""), @"\b(mecanico|mecanica|supervisor|supervisora)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));

    private bool ControlDocumentoTrainingAppliesTo(string person, string header) =>
        !IsMechanicSupervisorTraining(header) || BuildControlDocumentosModel().Filas.Any(row =>
            NormalizeControlDocumentoKey(row.Conductor) == NormalizeControlDocumentoKey(person) &&
            HasMechanicSupervisorPosition(row.Datos));

    private static ControlDocumentosCapacitacionEstadoViewModel NonApplicableTraining(
        string header, string message = "Exclusivo para mecánicos y supervisores.") => new()
    {
        NombreCapacitacion = header, Aplica = false, EstadoFiltro = "noaplica", EstadoSemaforo = "neutro",
        EtiquetaSemaforo = "No aplica", MensajeAlerta = message
    };

    private static void HideUnusedControlDocumentoTrainings(ControlDocumentosViewModel model)
    {
        var indices = Enumerable.Range(0, model.EncabezadosCapacitacion.Count)
            .Where(i => !IsMechanicSupervisorTraining(model.EncabezadosCapacitacion[i]) ||
                model.Filas.Any(row => row.Capacitaciones[i].Aplica)).ToList();
        model.EncabezadosCapacitacion = indices.Select(i => model.EncabezadosCapacitacion[i]).ToList();
        foreach (var row in model.Filas)
        {
            row.Capacitaciones = indices.Select(i => row.Capacitaciones[i]).ToList();
            row.FechasCapacitacion = indices.Select(i => row.FechasCapacitacion[i]).ToList();
            row.FechasCapacitacionIso = indices.Select(i => row.FechasCapacitacionIso[i]).ToList();
        }
    }
}
