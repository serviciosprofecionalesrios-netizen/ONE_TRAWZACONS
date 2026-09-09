using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class OperacionesControlDocumentosPdfReportService
    {
        public static byte[] GenerateConductorPdf(
            string conductor,
            IReadOnlyList<ControlDocumentosPdfCapacitacionRow> rows,
            string webRootPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var generatedAt = DateTime.Now;
            var logo = TryLoadLogo(webRootPath);
            var vigentes = rows.Count(r => string.Equals(r.Estado, "Vigente", StringComparison.OrdinalIgnoreCase));
            var porVencer = rows.Count(r => string.Equals(r.Estado, "Por vencer", StringComparison.OrdinalIgnoreCase));
            var vencidas = rows.Count(r => string.Equals(r.Estado, "Vencida", StringComparison.OrdinalIgnoreCase));
            var sinFecha = rows.Count(r => string.Equals(r.Estado, "Sin fecha", StringComparison.OrdinalIgnoreCase));

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(header => ComposeHeader(header, generatedAt, logo, conductor));
                    page.Content().Element(content => ComposeContent(content, rows, vigentes, porVencer, vencidas, sinFecha));
                    page.Footer().Element(footer => ComposeFooter(footer, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, DateTime generatedAt, byte[]? logo, string conductor)
        {
            container.BorderBottom(2)
                .BorderColor("#11439A")
                .PaddingBottom(8)
                .Row(row =>
                {
                    row.ConstantItem(90).Height(58).AlignMiddle().AlignCenter().Element(logoBox =>
                    {
                        if (logo != null)
                        {
                            logoBox.Image(logo).FitArea();
                        }
                        else
                        {
                            logoBox.Text("SIN LOGO").FontSize(8).FontColor(Colors.Grey.Darken1);
                        }
                    });

                    row.RelativeItem().PaddingLeft(10).Column(column =>
                    {
                        column.Item().Text("TRAWZACONS")
                            .FontSize(18)
                            .SemiBold()
                            .FontColor("#11439A");
                        column.Item().Text("Operaciones - Control de Documentos")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(2).Text($"Detalle conductor: {conductor}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(180).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("CONTROL DOCUMENTOS").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(
            IContainer container,
            IReadOnlyList<ControlDocumentosPdfCapacitacionRow> rows,
            int vigentes,
            int porVencer,
            int vencidas,
            int sinFecha)
        {
            container.PaddingTop(10).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(card => ComposeSectionCard(card, "Resumen", body =>
                {
                    body.Row(row =>
                    {
                        row.Spacing(8);
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Capacitaciones", rows.Count.ToString(CultureInfo.InvariantCulture), "#0B1D3A"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Vigentes", vigentes.ToString(CultureInfo.InvariantCulture), "#0B6E4F"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Por vencer", porVencer.ToString(CultureInfo.InvariantCulture), "#D97706"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Vencidas", vencidas.ToString(CultureInfo.InvariantCulture), "#11439A"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Sin fecha", sinFecha.ToString(CultureInfo.InvariantCulture), "#475569"));
                    });
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Detalle de capacitaciones", body =>
                {
                    body.Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.4f);
                            c.RelativeColumn(1f);
                            c.RelativeColumn(0.9f);
                            c.RelativeColumn(2f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Capacitacion");
                            header.Cell().Element(HeaderCell).Text("Fecha");
                            header.Cell().Element(HeaderCell).Text("Estado");
                            header.Cell().Element(HeaderCell).Text("Alerta");
                        });

                        foreach (var row in rows)
                        {
                            table.Cell().Element(BodyCell).Text(row.Capacitacion);
                            table.Cell().Element(BodyCell).Text(row.Fecha);
                            table.Cell().Element(BodyCell).Text(row.Estado);
                            table.Cell().Element(BodyCell).Text(row.Alerta);
                        }
                    });
                }));
            });
        }

        private static void ComposeFooter(IContainer container, DateTime generatedAt)
        {
            container.BorderTop(1)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingTop(5)
                .Row(row =>
                {
                    row.RelativeItem().Text($"Confidencial | {generatedAt:dd/MM/yyyy HH:mm}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);

                    row.ConstantItem(120).AlignRight().Text(text =>
                    {
                        text.Span("Pagina ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.CurrentPageNumber().FontSize(8).SemiBold();
                        text.Span(" de ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.TotalPages().FontSize(8).SemiBold();
                    });
                });
        }

        private static void ComposeSectionCard(IContainer container, string title, Action<IContainer> bodyComposer)
        {
            container.Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(Colors.White)
                .Padding(10)
                .Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(4).Height(16).Background("#11439A");
                        row.RelativeItem().PaddingLeft(8).Text(title)
                            .SemiBold()
                            .FontSize(12)
                            .FontColor(Colors.Grey.Darken4);
                    });

                    column.Item().PaddingTop(8).Element(bodyComposer);
                });
        }

        private static void ComposeMetricCard(IContainer container, string label, string value, string accentColor)
        {
            container.Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background("#FAFAFA")
                .Padding(8)
                .Column(column =>
                {
                    column.Spacing(3);
                    column.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                    column.Item().Text(value).FontSize(12).SemiBold().FontColor(accentColor);
                });
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor("#11439A")
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor("#11439A").FontSize(9));
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.FontSize(9));
        }

        private static byte[]? TryLoadLogo(string webRootPath)
        {
            var candidates = new[]
            {
                Path.Combine(webRootPath, "images", "logo-trawzacons-corporativo.png"),
                Path.Combine(webRootPath, "images", "logo-trawzacons.png")
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    return File.ReadAllBytes(path);
                }
                catch
                {
                    // Ignore and continue.
                }
            }

            return null;
        }
    }

    public sealed record ControlDocumentosPdfCapacitacionRow(
        string Capacitacion,
        string Fecha,
        string Estado,
        string Alerta);
}



