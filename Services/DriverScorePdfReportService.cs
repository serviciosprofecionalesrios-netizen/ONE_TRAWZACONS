using ITServiceDeskApp.ViewModels.Operaciones;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services;

public static class DriverScorePdfReportService
{
    public static byte[] Generate(DriverScoreViewModel model)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(doc => doc.Page(page => {
            page.Size(PageSizes.A4.Landscape()); page.Margin(25); page.DefaultTextStyle(x => x.FontSize(9));
            page.Header().Column(c => { c.Item().Text("ONE-TRAWZACONS · DRIVER SCORE").Bold().FontSize(18).FontColor("#11439A"); c.Item().Text($"Reporte gerencial | {model.From:dd/MM/yyyy} al {model.To:dd/MM/yyyy} | {model.SelectedDriver ?? "Todos los conductores"}"); });
            page.Content().PaddingTop(12).Column(c => {
                c.Spacing(12);
                if (!string.IsNullOrWhiteSpace(model.Warning)) c.Item().Text(model.Warning).FontColor("#92400E");
                c.Item().Text($"Infracciones: {model.TotalEvents}  |  Conductores en rojo: {model.CriticalDrivers}  |  A tiempo: {model.TimelyPercent:N1}%  |  Coaching pendiente: {model.CoachingPending}").Bold();
                c.Item().Text($"Mes anterior: {model.PreviousMonthEvents} eventos | Variación: {(model.MonthChange.HasValue ? model.MonthChange.Value.ToString("N1") + "%" : "Sin base comparable")}");
                c.Item().Text("Conductores y evaluación del período").Bold();
                c.Item().Table(t => { t.ColumnsDefinition(d => { d.RelativeColumn(3); d.RelativeColumn(); d.RelativeColumn(); d.RelativeColumn(); d.RelativeColumn(); d.RelativeColumn(); d.RelativeColumn(3); });
                    t.Header(h => { foreach(var label in new[] { "Conductor", "Unidad", "Eventos", "Puntos", "Km estimados", "Eventos/1,000 km", "Estado" }) h.Cell().Background("#DDEBF7").Padding(4).Text(label).Bold(); });
                    foreach(var r in model.Scorecards) { foreach(var text in new[] { r.Driver, r.Vehicle, r.Events.ToString(), r.Score.ToString(), r.Kilometers.ToString("N0"), r.Kilometers > 0 ? (1000m * r.Events / r.Kilometers).ToString("N2") : "Sin km", r.Status }) t.Cell().BorderBottom(.5f).BorderColor("#DDDDDD").Padding(4).Text(text); }
                });
                c.Item().Text("Eventos frecuentes").Bold();
                foreach(var e in model.EventDistribution) c.Item().Text($"{e.EventType}: {e.Count} | Unidades: {e.Vehicles}");
                c.Item().Text("Reincidencias después del coaching").Bold();
                foreach(var r in model.RepeatedAfterCoaching) c.Item().Text($"{r.Driver} · {r.Vehicle} · {r.EventType}: {r.Count} posteriores al registro {r.CoachingAt:dd/MM/yyyy}");
                c.Item().Text("Acciones pendientes: realizar coaching de los eventos pendientes y priorizar los conductores en rojo. Kilómetros estimados del período: 520 por registro Finalizado y 260 por En ruta. La fecha real de coaching no está disponible en la hoja.").FontColor("#555555");
                c.Item().Text($"Casos: {model.PendingCases} pendientes | {model.FollowingCases} en seguimiento | {model.ClosedCases} cerrados").Bold();
                foreach(var e in model.AllPeriodEvents.Where(x => model.CaseStatus(x) != "Cerrado")) {
                    model.Cases.TryGetValue(e.CaseKey, out var item);
                    c.Item().Text($"{e.Date:dd/MM/yyyy} · {e.Driver} · {e.Vehicle} · {e.EventType} | {model.CaseStatus(e)} | Responsable: {item?.Responsible ?? "Sin asignar"}");
                }
            });
            page.Footer().AlignRight().Text(t => { t.Span("Página "); t.CurrentPageNumber(); t.Span(" de "); t.TotalPages(); });
        })).GeneratePdf();
    }
}
