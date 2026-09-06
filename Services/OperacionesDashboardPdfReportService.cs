using System.Globalization;
using ITServiceDeskApp.ViewModels.Operaciones;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class OperacionesDashboardPdfReportService
    {
        public static byte[] GenerateDashboardPdf(OperacionesDashboardViewModel model, string webRootPath)
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

        private static void ComposeHeader(IContainer container, DateTime generatedAt, byte[]? logo, OperacionesDashboardViewModel model)
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
                        column.Item().Text(string.Empty)
                            .FontSize(18)
                            .SemiBold()
                            .FontColor("#11439A");
                        column.Item().Text("Operaciones - Reporte Dashboard")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(2).Text($"Fuente: {model.SourceFileName ?? "Seguimiento de toneladas"}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(210).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("DASHBOARD OPERACIONES").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(IContainer container, OperacionesDashboardViewModel model)
        {
            container.PaddingTop(10).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(card => ComposeSectionCard(card, "Resumen Operativo", body =>
                {
                    var turnoDia = model.CumplimientoPorTurno
                        .FirstOrDefault(x => string.Equals(x.Turno, "DIA", StringComparison.OrdinalIgnoreCase));
                    var turnoNoche = model.CumplimientoPorTurno
                        .FirstOrDefault(x => string.Equals(x.Turno, "NOCHE", StringComparison.OrdinalIgnoreCase));

                    body.Row(row =>
                    {
                        row.Spacing(8);
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Toneladas descargadas dia", model.ToneladasDescargadasDia.ToString("N1", CultureInfo.InvariantCulture), "#11439A"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Cumplimiento dia", $"{model.CumplimientoPlanPorcentaje}%", "#0B1D3A"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Descargadas TRITON", model.ToneladasDescargadasDiaTriton.ToString("N1", CultureInfo.InvariantCulture), "#0F766E"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Descargadas PAVON ASM", model.ToneladasDescargadasDiaPavonAsm.ToString("N1", CultureInfo.InvariantCulture), "#1D4ED8"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Toneladas mes", model.ToneladasMovilizadas.ToString("N1", CultureInfo.InvariantCulture), "#7C3AED"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Diesel mes (gal)", model.DieselConsumidoGalones.ToString("N2", CultureInfo.InvariantCulture), "#A16207"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Prediccion manana", model.PrediccionToneladasManana.ToString("N1", CultureInfo.InvariantCulture), "#0B6E4F"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Equipos finalizados", model.EquiposFinalizadosDia.ToString(CultureInfo.InvariantCulture), "#1D4ED8"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Equipos en ruta", model.EquiposEnRutaDia.ToString(CultureInfo.InvariantCulture), "#11439A"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Rendimiento T/GAL", model.RendimientoToneladasPorGalonMes.ToString("N3", CultureInfo.InvariantCulture), "#0F766E"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Meta mensual", model.MetaMensualToneladas.ToString("N1", CultureInfo.InvariantCulture), "#7C3AED"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Proyeccion cierre", model.ProyeccionCierreMensualToneladas.ToString("N1", CultureInfo.InvariantCulture), "#0B6E4F"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Cumpl. proy. mes", $"{model.CumplimientoProyectadoMensualPorcentaje}%", "#0B1D3A"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Turno DIA", $"{(turnoDia?.Toneladas ?? 0m):N1} Tn / {(turnoDia?.CumplimientoPorcentaje ?? 0)}%", "#1D4ED8"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Turno NOCHE", $"{(turnoNoche?.Toneladas ?? 0m):N1} Tn / {(turnoNoche?.CumplimientoPorcentaje ?? 0)}%", "#11439A"));
                    });
                }));

                column.Item().Row(row =>
                {
                    row.Spacing(10);

                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Estado y Alertas", body =>
                    {
                        body.Column(col =>
                        {
                            col.Spacing(4);
                            col.Item().Text($"Estado operativo: {model.EstadoOperativoLabel}")
                                .SemiBold()
                                .FontColor("#0B1D3A");
                            col.Item().Text($"Brecha meta hoy: {(model.BrechaMetaToneladas > 0 ? model.BrechaMetaToneladas.ToString("N1", CultureInfo.InvariantCulture) + " Tn" : "Meta cubierta")}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);

                            if (model.AlertasOperativas.Count == 0)
                            {
                                col.Item().Text("Sin alertas operativas.")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);
                            }
                            else
                            {
                                foreach (var alerta in model.AlertasOperativas.Take(5))
                                {
                                    col.Item().Text($"- {alerta}")
                                        .FontSize(9)
                                        .FontColor(Colors.Grey.Darken2);
                                }
                            }
                        });
                    }));

                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Brechas de Rendimiento por Equipo", body =>
                    {
                        ComposeDesviacionesTable(body, model.DesviacionesRendimiento);
                    }));
                });

                column.Item().Row(row =>
                {
                    row.Spacing(10);

                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Comparativo Toneladas (Ene-Feb-Mar)", body =>
                    {
                        ComposeComparativoTable(
                            body,
                            model.ComparativoToneladasSitio,
                            "TRITON (Tn)",
                            "PAVON ASM (Tn)",
                            valueFormat: v => v.ToString("N1", CultureInfo.InvariantCulture));
                    }));

                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Comparativo Diesel (Ene-Feb-Mar)", body =>
                    {
                        ComposeComparativoTable(
                            body,
                            model.ComparativoDieselSitio,
                            "TRITON (gal)",
                            "PAVON ASM (gal)",
                            valueFormat: v => v.ToString("N2", CultureInfo.InvariantCulture));
                    }));
                });

                column.Item().ShowEntire().Row(row =>
                {
                    row.Spacing(10);

                    row.RelativeItem().Element(card => ComposeSectionCard(card, $"Top Conductores ({model.TopConductores.Count})", body =>
                    {
                        ComposeRankingTable(
                            body,
                            model.TopConductores,
                            "Conductor");
                    }));

                    row.RelativeItem().Element(card => ComposeSectionCard(card, $"Top Equipos ({model.TopEquipos.Count})", body =>
                    {
                        ComposeRankingTable(
                            body,
                            model.TopEquipos,
                            "Equipo");
                    }));
                });
            });
        }

        private static void ComposeComparativoTable(
            IContainer container,
            IReadOnlyList<OperacionesDashboardComparativoMensualItemViewModel> rows,
            string colTriton,
            string colPavonAsm,
            Func<decimal, string> valueFormat)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1f);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(1.2f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Mes");
                    header.Cell().Element(HeaderCell).AlignRight().Text(colTriton);
                    header.Cell().Element(HeaderCell).AlignRight().Text(colPavonAsm);
                });

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan(3).Element(cell =>
                        BodyCell(cell).AlignCenter().Text("Sin datos para comparar."));
                    return;
                }

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).Text(row.Mes);
                    table.Cell().Element(BodyCell).AlignRight().Text(valueFormat(row.Triton));
                    table.Cell().Element(BodyCell).AlignRight().Text(valueFormat(row.PavonAsm));
                }
            });
        }

        private static void ComposeRankingTable(
            IContainer container,
            IReadOnlyList<OperacionesDashboardRankingItemViewModel> rows,
            string entityLabel)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(26);
                    c.RelativeColumn(2f);
                    c.RelativeColumn(1f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("#");
                    header.Cell().Element(HeaderCell).Text(entityLabel);
                    header.Cell().Element(HeaderCell).AlignRight().Text("Toneladas");
                });

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan(3).Element(cell =>
                        BodyCell(cell).AlignCenter().Text("Sin datos disponibles."));
                    return;
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    table.Cell().Element(BodyCell).Text((i + 1).ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).Text(row.Nombre);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.Toneladas.ToString("N1", CultureInfo.InvariantCulture));
                }
            });
        }

        private static void ComposeDesviacionesTable(
            IContainer container,
            IReadOnlyList<OperacionesDashboardDesviacionItemViewModel> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.9f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Equipo");
                    header.Cell().Element(HeaderCell).AlignRight().Text("T/GAL");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Var. %");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Toneladas");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Galones");
                });

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan(5).Element(cell =>
                        BodyCell(cell).AlignCenter().Text("Sin datos para desviaciones."));
                    return;
                }

                foreach (var row in rows.Take(8))
                {
                    table.Cell().Element(BodyCell).Text(row.Nombre);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.ToneladasPorGalon.ToString("N3", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.VariacionVsPromedioPorcentaje.ToString("N1", CultureInfo.InvariantCulture) + "%");
                    table.Cell().Element(BodyCell).AlignRight().Text(row.Toneladas.ToString("N1", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.Galones.ToString("N2", CultureInfo.InvariantCulture));
                }
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
}



