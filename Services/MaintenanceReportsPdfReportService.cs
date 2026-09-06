using System;
using System.IO;
using ITServiceDeskApp.ViewModels.MaintenanceReports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class MaintenanceReportsPdfReportService
    {
        public static byte[] GeneratePdf(MaintenanceReportsViewModel model, string webRootPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var generatedAt = DateTime.Now;
            var logoBytes = TryLoadLogo(webRootPath);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Header().Element(header => ComposeHeader(header, generatedAt, logoBytes));
                    page.Content().Element(content => ComposeContent(content, model));
                    page.Footer().AlignRight().Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, DateTime generatedAt, byte[]? logoBytes)
        {
            container.BorderBottom(1).BorderColor("#11439A").PaddingBottom(6).Row(row =>
            {
                row.ConstantItem(80).Height(45).AlignMiddle().AlignCenter().Element(slot =>
                {
                    if (logoBytes != null)
                    {
                        slot.Image(logoBytes).FitArea();
                    }
                    else
                    {
                        slot.Text("SIN LOGO").FontSize(8).FontColor(Colors.Grey.Darken1);
                    }
                });

                row.RelativeItem().PaddingLeft(8).Column(column =>
                {
                    column.Item().Text("Reporte de Mantenimiento").SemiBold().FontSize(16).FontColor("#11439A");
                    column.Item().Text("Falla a Falla, Elementos Dañados y Preventivo/Correctivo").FontSize(10);
                    column.Item().Text($"Periodo: {generatedAt:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }

        private static void ComposeContent(IContainer container, MaintenanceReportsViewModel model)
        {
            container.PaddingTop(10).Column(column =>
            {
                column.Spacing(8);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(card => MetricCard(card, "Tickets", model.TotalMaintenanceTickets.ToString()));
                    row.RelativeItem().Element(card => MetricCard(card, "Preventivos", model.PreventiveTickets.ToString()));
                    row.RelativeItem().Element(card => MetricCard(card, "Correctivos", model.CorrectiveTickets.ToString()));
                    row.RelativeItem().Element(card => MetricCard(card, "Eventos Falla-Falla", model.FailureToFailureEvents.ToString()));
                    row.RelativeItem().Element(card => MetricCard(card, "Promedio KM", model.AverageKmBetweenFailures?.ToString("N2") ?? "N/D"));
                });

                column.Item().Text("Top elementos dañados").SemiBold().FontSize(11);
                column.Item().Element(section =>
                {
                    section.Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Categoria");
                            header.Cell().Element(HeaderCell).Text("Elemento");
                            header.Cell().Element(HeaderCell).Text("Total");
                        });

                        foreach (var row in model.TopDamagedElements)
                        {
                            table.Cell().Element(BodyCell).Text(row.FailureCategory);
                            table.Cell().Element(BodyCell).Text(row.DamagedElement);
                            table.Cell().Element(BodyCell).Text(row.Total.ToString());
                        }
                    });
                });
            });
        }

        private static void MetricCard(IContainer container, string label, string value)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(column =>
            {
                column.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                column.Item().Text(value).SemiBold().FontSize(12).FontColor("#11439A");
            });
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor("#11439A")
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor("#11439A"));
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(3);
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
                    // Ignorar y continuar.
                }
            }

            return null;
        }
    }
}



