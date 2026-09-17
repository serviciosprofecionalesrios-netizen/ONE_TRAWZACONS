using ITServiceDeskApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services;

public static class InventoryPhysicalCountPdfReportService
{
    public static byte[] GeneratePdf(IReadOnlyList<MaintenanceInventoryPart> items, string webRootPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(18);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8));
            page.Header().Column(column =>
            {
                column.Item().Text("INVENTARIO FÍSICO - MAESTRO DE INVENTARIOS").SemiBold().FontSize(15).FontColor("#11439A");
                column.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm} | Registros: {items.Count}").FontColor(Colors.Grey.Darken1);
            });
            page.Content().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(1.3f); c.RelativeColumn(1.2f); c.RelativeColumn(2.8f); c.RelativeColumn(1.1f); c.RelativeColumn(.8f); c.RelativeColumn(1.2f); c.RelativeColumn(.8f); c.RelativeColumn(.9f); c.RelativeColumn(.9f); c.RelativeColumn(1.5f); });
                table.Header(h => { foreach (var label in new[] { "Ítem", "Código", "Artículo", "N.º parte", "Alm.", "Ubicación", "U/M", "Sistema", "Físico", "Obs." }) h.Cell().Element(HeaderCell).Text(label); });
                foreach (var item in items)
                {
                    table.Cell().Element(BodyCell).Text(item.ItemCode ?? "-"); table.Cell().Element(BodyCell).Text(item.PartCode); table.Cell().Element(BodyCell).Text(item.PartName);
                    table.Cell().Element(BodyCell).Text(item.ManufacturerPartNumber ?? "-"); table.Cell().Element(BodyCell).Text(item.Site); table.Cell().Element(BodyCell).Text(item.ShelfLocation ?? "-");
                    table.Cell().Element(BodyCell).Text(item.UnitOfMeasure); table.Cell().Element(BodyCell).AlignRight().Text(item.QuantityOnHand.ToString()); table.Cell().Element(BodyCell).MinHeight(18); table.Cell().Element(BodyCell).MinHeight(18);
                }
            });
            page.Footer().AlignRight().Text(x => { x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages(); });
        })).GeneratePdf();
    }

    private static IContainer HeaderCell(IContainer c) => c.Background("#DDEBF7").BorderBottom(1).BorderColor("#11439A").Padding(4).DefaultTextStyle(TextStyle.Default.SemiBold());
    private static IContainer BodyCell(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4);
}
