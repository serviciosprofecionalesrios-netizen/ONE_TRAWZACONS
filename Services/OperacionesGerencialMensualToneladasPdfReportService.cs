using System.Globalization;
using ITServiceDeskApp.ViewModels.Operaciones;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class OperacionesGerencialMensualToneladasPdfReportService
    {
        public static byte[] GenerateReportePdf(
            OperacionesReporteGerencialMensualToneladasViewModel model,
            string webRootPath)
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

                    page.Header().Element(h => ComposeHeader(h, generatedAt, logo, model));
                    page.Content().Element(c => ComposeContent(c, model));
                    page.Footer().Element(f => ComposeFooter(f, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(
            IContainer container,
            DateTime generatedAt,
            byte[]? logo,
            OperacionesReporteGerencialMensualToneladasViewModel model)
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
                        column.Item().Text("Operaciones - Reporte Gerencial Mensual de Toneladas")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(2)
                            .Text($"Periodo: {model.PeriodoLabel} | Corte operativo: {model.FechaCorte:dd/MM/yyyy}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken2);
                        column.Item().Text($"Fuente: {model.FuenteDatos}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(210).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("REPORTE GERENCIAL MENSUAL").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(IContainer container, OperacionesReporteGerencialMensualToneladasViewModel model)
        {
            container.PaddingTop(10).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(card => ComposeSectionCard(card, "Resumen ejecutivo", body =>
                {
                    body.Row(row =>
                    {
                        row.Spacing(8);
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Toneladas mes", $"{model.TotalToneladas:N1} Tn", "#0B1D3A"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Diesel mes", $"{model.TotalDieselGalones:N2} gal", "#0F766E"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Registros", model.TotalRegistros.ToString(CultureInfo.InvariantCulture), "#334155"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Meta diaria TRITON", $"{model.MetaDiariaTriton:N1}", "#11439A"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Meta diaria PAVON ASM", $"{model.MetaDiariaPavonAsm:N1}", "#1D4ED8"));
                    });
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Comportamiento mensual por sitio", body =>
                {
                    ComposeSitiosTable(body, model.Sitios);
                }));

                column.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Top 10 operadores (mas toneladas)", body =>
                    {
                        ComposeRankingTable(body, model.TopOperadores, "Sin operadores para el periodo.");
                    }));
                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Top 10 equipos (mas toneladas)", body =>
                    {
                        ComposeRankingTable(body, model.TopEquipos, "Sin equipos para el periodo.");
                    }));
                });

                column.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Bottom 5 conductores (menos toneladas)", body =>
                    {
                        ComposeRankingTable(body, model.BottomConductores, "Sin conductores para el periodo.");
                    }));
                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Bottom 5 equipos (menos toneladas)", body =>
                    {
                        ComposeRankingTable(body, model.BottomEquipos, "Sin equipos para el periodo.");
                    }));
                });

                column.Item().Element(card => ComposeSectionCard(card, "Cumplimiento de meta diaria por sitio", body =>
                {
                    ComposeCumplimientoSitioTable(body, model.CumplimientoPorSitio);
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Detalle de dias con meta cumplida por sitio", body =>
                {
                    ComposeDiasCumplidosTable(body, model.DiasMetaCumplidaDetalle);
                }));
            });
        }

        private static void ComposeSitiosTable(IContainer container, IReadOnlyList<OperacionesReporteSitioResumenViewModel> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.8f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Sitio");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Toneladas");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Diesel (gal)");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Meta mensual");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Cumpl. mensual");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Meta diaria");
                });

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan(6).Element(cell =>
                        BodyCell(cell).AlignCenter().Text("Sin datos por sitio para este periodo."));
                    return;
                }

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).Text(row.Sitio);
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.Toneladas:N1}");
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.DieselGalones:N2}");
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.MetaMensual:N1}");
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.CumplimientoMensualPct:N1}%");
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.MetaDiaria:N1}");
                }
            });
        }

        private static void ComposeRankingTable(
            IContainer container,
            IReadOnlyList<OperacionesReporteRankingRowViewModel> rows,
            string emptyMessage)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(28);
                    c.RelativeColumn(1.35f);
                    c.RelativeColumn(0.85f);
                    c.RelativeColumn(0.8f);
                    c.RelativeColumn(0.6f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).AlignCenter().Text("#");
                    header.Cell().Element(HeaderCell).Text("Nombre");
                    header.Cell().Element(HeaderCell).Text("Sitio");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Toneladas");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Viajes");
                });

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan(5).Element(cell => BodyCell(cell).AlignCenter().Text(emptyMessage));
                    return;
                }

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).AlignCenter().Text(row.Posicion.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).Text(row.Nombre);
                    table.Cell().Element(BodyCell).Text(row.Sitio);
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.Toneladas:N1}");
                    table.Cell().Element(BodyCell).AlignRight().Text(row.Viajes.ToString(CultureInfo.InvariantCulture));
                }
            });
        }

        private static void ComposeCumplimientoSitioTable(
            IContainer container,
            IReadOnlyList<OperacionesReporteCumplimientoDiaSitioViewModel> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.0f);
                    c.RelativeColumn(0.8f);
                    c.RelativeColumn(0.8f);
                    c.RelativeColumn(0.8f);
                    c.RelativeColumn(0.85f);
                    c.RelativeColumn(0.95f);
                    c.RelativeColumn(1.0f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Sitio");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Meta diaria");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Dias operados");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Dias cumplidos");
                    header.Cell().Element(HeaderCell).AlignRight().Text("% dias cumpl.");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Promedio dia");
                    header.Cell().Element(HeaderCell).Text("Mejor dia");
                });

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan(7).Element(cell =>
                        BodyCell(cell).AlignCenter().Text("Sin datos de cumplimiento por sitio."));
                    return;
                }

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).Text(row.Sitio);
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.MetaDiaria:N1}");
                    table.Cell().Element(BodyCell).AlignRight().Text(row.DiasConOperacion.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.DiasMetaCumplida.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.CumplimientoDiasPct:N1}%");
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.PromedioToneladasDia:N1}");
                    table.Cell().Element(BodyCell).Text(
                        row.FechaMejorDia.HasValue
                            ? $"{row.FechaMejorDia.Value:dd/MM} ({row.MejorDiaToneladas:N1} Tn)"
                            : "Sin datos");
                }
            });
        }

        private static void ComposeDiasCumplidosTable(
            IContainer container,
            IReadOnlyList<OperacionesReporteDiaCumplidoViewModel> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.8f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.9f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Sitio");
                    header.Cell().Element(HeaderCell).Text("Fecha");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Toneladas");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Meta diaria");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Cumplimiento");
                });

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan(5).Element(cell =>
                        BodyCell(cell).AlignCenter().Text("No hubo dias con meta cumplida para el filtro mensual."));
                    return;
                }

                foreach (var row in rows.OrderBy(x => x.Sitio).ThenBy(x => x.Fecha))
                {
                    table.Cell().Element(BodyCell).Text(row.Sitio);
                    table.Cell().Element(BodyCell).Text(row.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.Toneladas:N1}");
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.MetaDiaria:N1}");
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.CumplimientoPct:N1}%");
                }
            });
        }

        private static void ComposeSectionCard(IContainer container, string title, Action<IContainer> bodyComposer)
        {
            container.Border(1)
                .BorderColor("#D7E3F3")
                .Background("#F8FBFF")
                .Padding(10)
                .Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(4).Height(16).Background("#11439A");
                        row.RelativeItem().PaddingLeft(8).Text(title)
                            .SemiBold()
                            .FontSize(12)
                            .FontColor("#1D2D44");
                    });

                    column.Item().PaddingTop(8).Element(bodyComposer);
                });
        }

        private static void ComposeMetricCard(IContainer container, string label, string value, string accentColor)
        {
            container.Border(1)
                .BorderColor("#D7E3F3")
                .Background("#FFFFFF")
                .Padding(8)
                .Column(column =>
                {
                    column.Spacing(3);
                    column.Item().Text(label).FontSize(8).SemiBold().FontColor("#64748B");
                    column.Item().Text(value).FontSize(13).SemiBold().FontColor(accentColor);
                });
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor("#D7E3F3")
                .Background("#F3F6FB")
                .PaddingVertical(5)
                .PaddingHorizontal(6)
                .DefaultTextStyle(x => x.SemiBold().FontSize(8.5f).FontColor("#1D2D44"));
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor("#EDF2FA")
                .PaddingVertical(4)
                .PaddingHorizontal(6)
                .DefaultTextStyle(x => x.FontSize(8).FontColor("#1F2937"));
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



