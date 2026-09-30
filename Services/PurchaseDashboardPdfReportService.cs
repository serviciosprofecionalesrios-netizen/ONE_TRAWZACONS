using ITServiceDeskApp.ViewModels.Inventory;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services;

public static class PurchaseDashboardPdfReportService
{
    public static byte[] GeneratePdf(PurchaseDashboardViewModel model, string webRootPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(22);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));
            page.Header().BorderBottom(2).BorderColor("#11439A").PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("TRAWZACONS").FontSize(17).SemiBold().FontColor("#11439A");
                    column.Item().Text("Dashboard de órdenes de compra").FontSize(12).SemiBold();
                    column.Item().Text($"Período: {(model.From?.ToString("dd/MM/yyyy") ?? "Inicio")} al {(model.To?.ToString("dd/MM/yyyy") ?? "Actualidad")}").FontColor(Colors.Grey.Darken2);
                });
                row.ConstantItem(160).AlignRight().Column(column =>
                {
                    column.Item().Text("REPORTE DE COMPRAS").SemiBold();
                    column.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
            page.Content().PaddingTop(12).Column(column =>
            {
                column.Spacing(10);
                column.Item().Row(row =>
                {
                    Metric(row.RelativeItem(), "Órdenes", model.PurchaseOrders.ToString());
                    Metric(row.RelativeItem(), "Gasto C$", $"C$ {model.TotalCordobas:N2}");
                    Metric(row.RelativeItem(), "Gasto USD", $"$ {model.TotalUsd:N2}");
                    Metric(row.RelativeItem(), "Última compra", model.LatestPurchaseDate?.ToString("dd/MM/yyyy") ?? "Sin fecha");
                });
                column.Item().Text("Artículos de mayor gasto").FontSize(12).SemiBold().FontColor("#0B1D3A");
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.RelativeColumn(4); columns.RelativeColumn(2); columns.RelativeColumn(); columns.RelativeColumn(2); columns.RelativeColumn(2); });
                    Header(table, "Artículo", "Categoría", "OC", "C$", "USD");
                    foreach (var item in model.TopItems)
                        Row(table, item.Name, item.Category, item.Orders.ToString(), $"C$ {item.Cordobas:N2}", $"$ {item.Usd:N2}");
                });
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text("Gasto por categoría").FontSize(12).SemiBold();
                        left.Item().Table(table => { table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); }); Header(table, "Categoría", "C$", "USD"); foreach (var x in model.TopCategories) Row(table, x.Name, $"C$ {x.Cordobas:N2}", $"$ {x.Usd:N2}"); });
                    });
                    row.RelativeItem().PaddingLeft(10).Column(right =>
                    {
                        right.Item().Text("Principales proveedores").FontSize(12).SemiBold();
                        right.Item().Table(table => { table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(2); c.RelativeColumn(2); }); Header(table, "Proveedor", "OC", "C$", "USD"); foreach (var x in model.TopSuppliers.Take(6)) Row(table, x.Name, x.Orders.ToString(), $"C$ {x.Cordobas:N2}", $"$ {x.Usd:N2}"); });
                    });
                });
            });
            page.Footer().AlignCenter().Text("TRAWZACONS · Control de compras").FontSize(8).FontColor(Colors.Grey.Darken1);
        })).GeneratePdf();
    }

    private static void Metric(IContainer container, string label, string value) => container.Border(1).BorderColor("#D7E3F3").Padding(7).Column(c => { c.Item().Text(label).FontSize(8).FontColor("#526178"); c.Item().Text(value).FontSize(14).SemiBold().FontColor("#11439A"); });
    private static void Header(TableDescriptor table, params string[] values) { table.Header(header => { foreach (var value in values) header.Cell().Background("#11439A").Padding(4).Text(value).FontColor(Colors.White).SemiBold(); }); }
    private static void Row(TableDescriptor table, params string[] values) { foreach (var value in values) table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(4).Text(value); }
}
