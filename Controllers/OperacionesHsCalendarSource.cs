using ITServiceDeskApp.ViewModels.Operaciones;
namespace ITServiceDeskApp.Controllers;
public partial class OperacionesController
{
    // Internal adapter preserves the official merge, identity and validity rules.
    [Microsoft.AspNetCore.Mvc.NonAction]
    internal ControlDocumentosViewModel ReadHsCalendarDocuments() => BuildControlDocumentosModelCore(includeInactive: false, calendarOnly: true);
    internal static bool IsCalendarTraining(string header) => ControlDocumentoValidityYears(header).HasValue;
}
