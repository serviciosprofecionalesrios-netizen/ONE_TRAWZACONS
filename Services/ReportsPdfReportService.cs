using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.ViewModels.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class ReportsPdfReportService
    {
        public static byte[] GenerateReportPdf(
            ReportIndexViewModel model,
            IReadOnlyList<ReportsPdfTicketRow> tickets,
            string webRootPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var generatedAt = DateTime.Now;
            var logo = TryLoadLogo(webRootPath);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(header => ComposeHeader(header, generatedAt, logo));
                    page.Content().Element(content => ComposeContent(content, model, tickets));
                    page.Footer().Element(footer => ComposeFooter(footer, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, DateTime generatedAt, byte[]? logo)
        {
            container.BorderBottom(2)
                .BorderColor("#11439A")
                .PaddingBottom(8)
                .Row(row =>
                {
                    row.ConstantItem(90).Height(64).AlignMiddle().AlignCenter().Element(logoBox =>
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
                        column.Item().Text("Service Desk - Reporte Ejecutivo")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(2).Text("Modulo de Reportes")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(150).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("REPORTE OPERATIVO").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(IContainer container, ReportIndexViewModel model, IReadOnlyList<ReportsPdfTicketRow> tickets)
        {
            container.PaddingTop(12).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(card => ComposeSectionCard(card, "Resumen Corporativo", body =>
                {
                    body.Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text(BuildSummaryText(model));
                        col.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Total Tickets", model.TotalTickets.ToString(), "#0B1D3A"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Resueltos", model.ResolvedTickets.ToString(), "#0B6E4F"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Vencidos", model.OverdueTickets.ToString(), "#11439A"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Por vencer", model.NearDueTickets.ToString(), "#D97706"));
                        });
                        col.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "SLA", $"{model.SlaCompliancePercent}%", "#0B6E4F"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Primera respuesta", $"{model.AverageFirstResponseHours:0.0} hrs", "#2563EB"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "MTTR", $"{model.AverageResolutionHours:0.0} hrs", "#7C3AED"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Reaperturas", model.ReopenedTickets.ToString(), "#B45309"));
                        });
                    });
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Filtros Aplicados", body =>
                {
                    var rows = new List<ReportRow>
                    {
                        new("Fecha Inicio", model.StartDate.ToString("dd/MM/yyyy")),
                        new("Fecha Fin", model.EndDate.ToString("dd/MM/yyyy")),
                        new("Tecnico", OptionLabel(model.SelectedTechnician)),
                        new("Area", OptionLabel(model.SelectedArea)),
                        new("Sitio", OptionLabel(model.SelectedSite)),
                        new("Vista", OptionLabel(model.ActiveTab)),
                        new("Backlog Delta", model.BacklogDelta.ToString("+0;-0;0")),
                        new("Costo Total Est. (C$)", model.EstimatedTotalCostCordoba.ToString("N2"))
                    };
                    ComposeKeyValueTable(body, rows);
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Distribucion de Incidencias", body =>
                {
                    body.Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Element(x => ComposeGroupTable(x, "Por Tipo", model.IncidentTypeDistribution));
                        col.Item().Element(x => ComposeGroupTable(x, "Por Tecnico", model.TicketsByTechnician));
                        col.Item().Element(x => ComposeGroupTable(x, "Por Area", model.TicketsByArea));
                        col.Item().Element(x => ComposeGroupTable(x, "Por Sitio", model.TicketsBySite));
                    });
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Detalle de Tickets", body =>
                {
                    if (tickets.Count == 0)
                    {
                        body.Text("No hay tickets para los filtros seleccionados.")
                            .FontColor(Colors.Grey.Darken1);
                        return;
                    }

                    ComposeTicketsTable(body, tickets);
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

        private static void ComposeKeyValueTable(IContainer container, IReadOnlyList<ReportRow> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                });

                foreach (var row in rows)
                {
                    table.Cell().Element(CellLabel).Text(row.Label);
                    table.Cell().Element(CellValue).Text(row.Value);
                }
            });
        }

        private static void ComposeGroupTable(IContainer container, string title, IReadOnlyList<ReportGroupItemViewModel> rows)
        {
            container.Border(1)
                .BorderColor(Colors.Grey.Lighten3)
                .Padding(8)
                .Column(column =>
                {
                    column.Item().Text(title).SemiBold().FontSize(10).FontColor("#11439A");

                    if (rows.Count == 0)
                    {
                        column.Item().PaddingTop(4).Text("Sin datos").FontColor(Colors.Grey.Darken1);
                        return;
                    }

                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Categoria");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Tickets");
                            header.Cell().Element(HeaderCell).AlignRight().Text("%");
                        });

                        foreach (var row in rows)
                        {
                            table.Cell().Element(BodyCell).Text(row.Label);
                            table.Cell().Element(BodyCell).AlignRight().Text(row.Count.ToString());
                            table.Cell().Element(BodyCell).AlignRight().Text($"{row.Percentage:0.0}%");
                        }
                    });
                });
        }

        private static void ComposeTicketsTable(IContainer container, IReadOnlyList<ReportsPdfTicketRow> tickets)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(2f);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(1.8f);
                    c.RelativeColumn(1.8f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Ticket");
                    header.Cell().Element(HeaderCell).Text("Fecha");
                    header.Cell().Element(HeaderCell).Text("Estado");
                    header.Cell().Element(HeaderCell).Text("Prioridad");
                    header.Cell().Element(HeaderCell).Text("Tecnico");
                    header.Cell().Element(HeaderCell).Text("Area");
                    header.Cell().Element(HeaderCell).Text("Sitio");
                });

                foreach (var ticket in tickets.OrderByDescending(t => t.CreatedDate))
                {
                    table.Cell().Element(BodyCell).Text(ticket.TicketNumber);
                    table.Cell().Element(BodyCell).Text(ticket.CreatedDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    table.Cell().Element(BodyCell).Text(ticket.StatusLabel);
                    table.Cell().Element(BodyCell).Text(ticket.PriorityLabel);
                    table.Cell().Element(BodyCell).Text(ticket.Technician);
                    table.Cell().Element(BodyCell).Text(ticket.Area);
                    table.Cell().Element(BodyCell).Text(ticket.Site);
                }
            });
        }

        private static IContainer CellLabel(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(5)
                .PaddingRight(8)
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor(Colors.Grey.Darken2));
        }

        private static IContainer CellValue(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(5)
                .DefaultTextStyle(TextStyle.Default.FontColor(Colors.Grey.Darken4));
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

        private static string BuildSummaryText(ReportIndexViewModel model)
        {
            return
                $"El periodo evaluado comprende del {model.StartDate:dd/MM/yyyy} al {model.EndDate:dd/MM/yyyy}. " +
                $"Durante este intervalo se registraron {model.TotalTickets} incidencias, con {model.ResolvedTickets} resueltas " +
                $"y un cumplimiento SLA de {model.SlaCompliancePercent}%. La primera respuesta promedio fue {model.AverageFirstResponseHours:0.0} horas, " +
                $"con MTTR de {model.AverageResolutionHours:0.0} horas. El backlog vario {model.BacklogDelta:+0;-0;0} y el costo estimado fue C$ {model.EstimatedTotalCostCordoba:N2}.";
        }

        private static string OptionLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return "Todos";
            }

            if (value.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
            {
                return "Sin asignar";
            }

            return value;
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

        private sealed record ReportRow(string Label, string Value);
    }

    public sealed record ReportsPdfTicketRow(
        string TicketNumber,
        DateTime CreatedDate,
        string StatusLabel,
        string PriorityLabel,
        string Technician,
        string Area,
        string Site);
}



