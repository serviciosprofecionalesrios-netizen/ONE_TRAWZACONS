using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels.Operaciones;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
namespace ITServiceDeskApp.Services;
public static class DriverScoreEventPdfService
{
    public static byte[] Generate(DriverScoreEventViewModel incident, DriverScoreCase? item, bool closure)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(doc => doc.Page(page => {
            page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Column(c => { c.Item().Text("ONE-TRAWZACONS · DRIVER SCORE").Bold().FontSize(18).FontColor("#11439A"); c.Item().Text(closure ? "Reporte de cierre · Pasos 1 y 2 completos" : "Registro de infracción · Paso 1 completo"); });
            page.Content().PaddingTop(20).Column(c => {
                c.Spacing(10);
                c.Item().Text("PASO 1 · REGISTRO").Bold().FontColor("#11439A");
                c.Item().Text($"Fecha del evento: {incident.Date:dd/MM/yyyy HH:mm}");
                c.Item().Text($"Conductor: {incident.Driver}\nUnidad: {incident.Vehicle}\nInfracción: {incident.EventType}");
                c.Item().Text($"Grupo: {incident.Group}\nUbicación: {incident.Location}");
                c.Item().Text($"Descripción: {incident.Observation}");
                c.Item().Text($"Gestión: {(incident.Timely ? "A TIEMPO" : "FUERA DE TIEMPO")}\nCoaching registrado: {(incident.Coaching ? "Sí" : "No")}");
                c.Item().Text($"Sitio: {item?.Site ?? "Sin asignar"}\nResponsable: {item?.Responsible ?? "Sin asignar"}");
                if (closure) {
                    c.Item().Text("PASO 2 · CIERRE").Bold().FontColor("#11439A");
                    c.Item().Text($"Estado: {item!.Status}\nAcciones y observaciones: {item.Notes ?? "Sin observaciones"}");
                    c.Item().Text($"Evidencia adjunta: {item.EvidenceName}\nÚltima actualización: {item.UpdatedAt.AddHours(-6):dd/MM/yyyy HH:mm}\nRegistrado por: {item.UpdatedBy}");
                }
                c.Item().Text("El registro inicial solo puede ser modificado por el administrador.").FontSize(9).FontColor("#555555");
            });
            page.Footer().AlignRight().Text(t => { t.Span("Página "); t.CurrentPageNumber(); t.Span(" de "); t.TotalPages(); });
        })).GeneratePdf();
    }
}
