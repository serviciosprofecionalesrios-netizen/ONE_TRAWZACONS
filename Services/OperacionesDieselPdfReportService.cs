using System.Globalization;
using ITServiceDeskApp.ViewModels.Operaciones;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class OperacionesDieselPdfReportService
    {
        public static byte[] GenerateSeguimientoPdf(SeguimientoDieselViewModel model, string webRootPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var generatedAt = DateTime.Now;
            var logo = TryLoadLogo(webRootPath);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(header => ComposeHeader(header, generatedAt, logo, model));
                    page.Content().Element(content => ComposeContent(content, model));
                    page.Footer().Element(footer => ComposeFooter(footer, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, DateTime generatedAt, byte[]? logo, SeguimientoDieselViewModel model)
        {
            var fechaOperativa = model.FechaOperativa?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha";

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
                        column.Item().Text(string.Empty)
                            .FontSize(18)
                            .SemiBold()
                            .FontColor("#11439A");
                        column.Item().Text("Operaciones - Reporte de Seguimiento de Diesel")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(2).Text($"Corte operativo: {fechaOperativa}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(190).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("REPORTE OPERACIONES").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(IContainer container, SeguimientoDieselViewModel model)
        {
            container.PaddingTop(10).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(card => ComposeSectionCard(card, "Resumen Diesel", body =>
                {
                    body.Row(row =>
                    {
                        row.Spacing(8);
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Total galones", model.TotalGalones.ToString("N2", CultureInfo.InvariantCulture), "#11439A"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Total litros", model.TotalLitrosEquivalentes.ToString("N2", CultureInfo.InvariantCulture), "#0B6E4F"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Rendimiento t/gal", model.RendimientoPromedioTonPorGalon.ToString("N3", CultureInfo.InvariantCulture), "#0B1D3A"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Intensidad L/t", model.IntensidadLitrosPorTonelada.ToString("N3", CultureInfo.InvariantCulture), "#7C3AED"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "% sobreconsumo", $"{model.PorcentajeEquiposSobreconsumo.ToString("N1", CultureInfo.InvariantCulture)}%", "#0B2F75"));
                    });
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Detalle por unidad (eficiencia y accion operativa)", body =>
                {
                    ComposeTable(body, model.Rows);
                }));
            });
        }

        private static void ComposeTable(IContainer container, IReadOnlyList<SeguimientoDieselRowViewModel> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(22);      // #
                    c.RelativeColumn(0.70f);   // Unidad
                    c.RelativeColumn(1.00f);   // Ruta
                    c.RelativeColumn(0.78f);   // Estado consumo
                    c.RelativeColumn(0.78f);   // Estado operativo
                    c.RelativeColumn(0.62f);   // Galones
                    c.RelativeColumn(0.62f);   // Toneladas
                    c.RelativeColumn(0.55f);   // t/gal
                    c.RelativeColumn(0.55f);   // L/t
                    c.RelativeColumn(0.55f);   // Var. %
                    c.RelativeColumn(0.95f);   // Accion
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).AlignRight().Text("#");
                    header.Cell().Element(HeaderCell).Text("Unidad");
                    header.Cell().Element(HeaderCell).Text("Ruta");
                    header.Cell().Element(HeaderCell).Text("Estado consumo");
                    header.Cell().Element(HeaderCell).Text("Estado operativo");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Galones");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Toneladas");
                    header.Cell().Element(HeaderCell).AlignRight().Text("t/gal");
                    header.Cell().Element(HeaderCell).AlignRight().Text("L/t");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Var. %");
                    header.Cell().Element(HeaderCell).Text("Accion");
                });

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan(11).Element(cell =>
                        BodyCell(cell).AlignCenter().Text("No hay datos para exportar. Carga un archivo en Seguimiento de toneladas."));
                    return;
                }

                var index = 0;
                foreach (var row in rows)
                {
                    index++;
                    table.Cell().Element(BodyCell).AlignRight().Text(index.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).Text(row.Unidad);
                    table.Cell().Element(BodyCell).Text(FormatRouteForPdf(row.RutaPrincipal));
                    table.Cell().Element(BodyCell).Text(row.EstadoConsumo);
                    table.Cell().Element(BodyCell).Text(row.Estado);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.GalonesDespachados.ToString("N2", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.ToneladasMovilizadas.ToString("N2", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.ToneladasPorGalon.ToString("N3", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.LitrosPorTonelada.ToString("N3", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.VariacionVsPromedioPorcentaje.ToString("N1", CultureInfo.InvariantCulture)}%");
                    table.Cell().Element(BodyCell).Text(ResolveConsumoRecommendation(row.EstadoConsumo));
                }
            });
        }

        private static string FormatRouteForPdf(string? ruta)
        {
            var text = (ruta ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Sin ruta";
            }

            text = text.Replace(" - ", " / ", StringComparison.Ordinal);
            if (text.Length > 34)
            {
                return text[..31] + "...";
            }

            return text;
        }

        private static string ResolveConsumoRecommendation(string? estadoConsumo)
        {
            if (string.Equals(estadoConsumo, "CRITICO", StringComparison.OrdinalIgnoreCase))
            {
                return "Revision inmediata";
            }

            if (string.Equals(estadoConsumo, "VIGILAR", StringComparison.OrdinalIgnoreCase))
            {
                return "Seguimiento cercano";
            }

            return "Mantener practica";
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
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor("#11439A").FontSize(8));
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.FontSize(8.3f));
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
}



