using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ITServiceDeskApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class InventoryPdfReportService
    {
        public static byte[] GenerateInventoryReportPdf(IReadOnlyList<InventoryItem> items, string webRootPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var generatedAt = DateTime.Now;
            var logo = TryLoadLogo(webRootPath);
            var data = items ?? Array.Empty<InventoryItem>();

            var activeCount = data.Count(i => i.IsActive);
            var inactiveCount = data.Count - activeCount;
            var assignedCount = data.Count(i => !string.IsNullOrWhiteSpace(i.AssignedTo));

            var byCategory = data
                .GroupBy(i => Safe(i.Category, "Sin categoria"))
                .Select(g => new GroupRow(g.Key, g.Count()))
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Name)
                .ToList();

            var byStatus = data
                .GroupBy(i => Safe(i.Status, "Sin estado"))
                .Select(g => new GroupRow(g.Key, g.Count()))
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Name)
                .ToList();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(header => ComposeHeader(header, generatedAt, logo));
                    page.Content().Element(content => ComposeContent(content, data, activeCount, inactiveCount, assignedCount, byCategory, byStatus));
                    page.Footer().Element(footer => ComposeFooter(footer, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, DateTime generatedAt, byte[]? logo)
        {
            container.BorderBottom(2)
                .BorderColor("#C91010")
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
                        column.Item().Text("GRUPO TRANSAN")
                            .FontSize(18)
                            .SemiBold()
                            .FontColor("#C91010");
                        column.Item().Text("Service Desk - Reporte Ejecutivo de Inventario TI")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(2).Text("Control corporativo de activos tecnologicos")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(170).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("REPORTE INVENTARIO TI").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(
            IContainer container,
            IReadOnlyList<InventoryItem> items,
            int activeCount,
            int inactiveCount,
            int assignedCount,
            IReadOnlyList<GroupRow> byCategory,
            IReadOnlyList<GroupRow> byStatus)
        {
            container.PaddingTop(10).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(card => ComposeSectionCard(card, "Resumen Corporativo", body =>
                {
                    body.Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text(BuildSummaryText(items.Count, activeCount, inactiveCount, assignedCount));
                        col.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Total Activos", items.Count.ToString(), "#0B1D3A"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Activos", activeCount.ToString(), "#0B6E4F"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Inactivos", inactiveCount.ToString(), "#C91010"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Asignados", assignedCount.ToString(), "#2563EB"));
                        });
                    });
                }));

                column.Item().Row(row =>
                {
                    row.Spacing(10);

                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Distribucion por Categoria", body =>
                    {
                        ComposeGroupTable(body, byCategory);
                    }));

                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Distribucion por Estado", body =>
                    {
                        ComposeGroupTable(body, byStatus);
                    }));
                });

                column.Item().Element(card => ComposeSectionCard(card, "Detalle de Inventario", body =>
                {
                    if (items.Count == 0)
                    {
                        body.Text("No hay activos para los filtros seleccionados.")
                            .FontColor(Colors.Grey.Darken1);
                        return;
                    }

                    ComposeInventoryTable(body, items);
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
                    row.RelativeItem().Text($"Confidencial - Grupo Transan | {generatedAt:dd/MM/yyyy HH:mm}")
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
                        row.ConstantItem(4).Height(16).Background("#C91010");
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

        private static void ComposeGroupTable(IContainer container, IReadOnlyList<GroupRow> rows)
        {
            if (rows.Count == 0)
            {
                container.Text("Sin datos.").FontColor(Colors.Grey.Darken1);
                return;
            }

            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Categoria");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                });

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).Text(row.Name);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.Count.ToString(CultureInfo.InvariantCulture));
                }
            });
        }

        private static void ComposeInventoryTable(IContainer container, IReadOnlyList<InventoryItem> items)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.6f); // Codigo
                    c.RelativeColumn(1.6f); // Categoria
                    c.RelativeColumn(1.8f); // Marca/Modelo
                    c.RelativeColumn(1.4f); // Serie
                    c.RelativeColumn(1.5f); // Estado
                    c.RelativeColumn(1.8f); // Sitio
                    c.RelativeColumn(1.8f); // Departamento
                    c.RelativeColumn(1.8f); // Asignado
                    c.RelativeColumn(1.2f); // C$ 
                    c.RelativeColumn(1.2f); // $ 
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Codigo");
                    header.Cell().Element(HeaderCell).Text("Categoria");
                    header.Cell().Element(HeaderCell).Text("Marca / Modelo");
                    header.Cell().Element(HeaderCell).Text("Serie");
                    header.Cell().Element(HeaderCell).Text("Estado");
                    header.Cell().Element(HeaderCell).Text("Sitio");
                    header.Cell().Element(HeaderCell).Text("Departamento");
                    header.Cell().Element(HeaderCell).Text("Asignado");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Costo C$");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Costo $");
                });

                foreach (var item in items.OrderBy(i => i.Category).ThenBy(i => i.AssetCode))
                {
                    table.Cell().Element(BodyCell).Text(Safe(item.AssetCode, "-"));
                    table.Cell().Element(BodyCell).Text(Safe(item.Category, "-"));
                    table.Cell().Element(BodyCell).Text($"{Safe(item.Brand, "-")} / {Safe(item.Model, "-")}");
                    table.Cell().Element(BodyCell).Text(Safe(item.SerialNumber, "-"));
                    table.Cell().Element(BodyCell).Text(item.IsActive ? Safe(item.Status, "Activo") : "Inactivo");
                    table.Cell().Element(BodyCell).Text(Safe(item.Site, "-"));
                    table.Cell().Element(BodyCell).Text(Safe(item.Department, "-"));
                    table.Cell().Element(BodyCell).Text(Safe(item.AssignedTo, "Sin asignar"));
                    table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(item.CostCordoba, "C$"));
                    table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(item.CostUsd, "$"));
                }
            });
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor("#C91010")
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor("#C91010").FontSize(9));
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.FontSize(8.7f));
        }

        private static string BuildSummaryText(int total, int active, int inactive, int assigned)
        {
            return $"El inventario registra {total} activos de TI. Actualmente {active} se encuentran activos, {inactive} inactivos y {assigned} con asignacion a usuario o area.";
        }

        private static string Safe(string? value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }

        private static string FormatMoney(decimal? value, string symbol)
        {
            return value.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0} {1:N2}", symbol, value.Value)
                : "-";
        }

        private static byte[]? TryLoadLogo(string webRootPath)
        {
            var candidates = new[]
            {
                Path.Combine(webRootPath, "images", "logo-transan-corporativo.png"),
                Path.Combine(webRootPath, "images", "logo-transan.png")
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

        private sealed record GroupRow(string Name, int Count);
    }
}
