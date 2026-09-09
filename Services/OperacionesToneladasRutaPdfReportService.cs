using System.Globalization;
using ITServiceDeskApp.ViewModels.Operaciones;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class OperacionesToneladasRutaPdfReportService
    {
        private const decimal LitrosPorGalon = 3.78541m;

        public static byte[] GenerateReportePdf(
            SeguimientoToneladasViewModel model,
            string webRootPath,
            decimal metaMensualObjetivo)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var generatedAt = DateTime.Now;
            var logo = TryLoadLogo(webRootPath);

            var rowsConFecha = model.Rows
                .Where(r => r.FechaEvento.HasValue)
                .ToList();

            var fechaCorte = model.FechaOperativa
                ?? (rowsConFecha.Any()
                    ? rowsConFecha.Max(r => r.FechaEvento!.Value.Date)
                    : DateTime.Now.Date);

            var rowsMes = rowsConFecha
                .Where(r =>
                    r.FechaEvento!.Value.Year == fechaCorte.Year &&
                    r.FechaEvento.Value.Month == fechaCorte.Month)
                .ToList();

            if (!rowsMes.Any())
            {
                rowsMes = model.Rows.ToList();
            }

            var acumuladoMes = BuildAcumuladoMesPorRuta(rowsMes);
            var comparativoMensual = BuildComparativoMensualToneladas(rowsConFecha, model.Rows, fechaCorte);
            var metaMensual = metaMensualObjetivo > 0m
                ? metaMensualObjetivo
                : model.MetaMensualTotal;
            var resumen = BuildResumenAnalitico(acumuladoMes, comparativoMensual, fechaCorte, metaMensual);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(header => ComposeHeader(header, generatedAt, logo, model, fechaCorte));
                    page.Content().Element(content => ComposeContent(content, acumuladoMes, comparativoMensual, fechaCorte, resumen));
                    page.Footer().Element(footer => ComposeFooter(footer, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(
            IContainer container,
            DateTime generatedAt,
            byte[]? logo,
            SeguimientoToneladasViewModel model,
            DateTime fechaCorte)
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
                        column.Item().Text("Operaciones - Reporte de Toneladas por Ruta")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(2).Text($"Fuente: {model.SourceFileName ?? "Seguimiento de toneladas"}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken2);
                        column.Item().Text($"Corte operativo: {fechaCorte:dd/MM/yyyy}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(200).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("REPORTE TONELADAS POR RUTA").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(
            IContainer container,
            IReadOnlyList<RutaAcumuladoRow> acumuladoMes,
            IReadOnlyList<ComparativoMesRow> comparativoMensual,
            DateTime fechaCorte,
            ResumenAnalitico resumen)
        {
            container.PaddingTop(10).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(card => ComposeSectionCard(card, "Resumen mensual por ruta", body =>
                {
                    body.Row(row =>
                    {
                        row.Spacing(8);

                        foreach (var item in acumuladoMes)
                        {
                            row.RelativeItem().Element(c => ComposeMetricCard(
                                c,
                                item.Ruta,
                                item.ToneladasMes.ToString("N1", CultureInfo.InvariantCulture),
                                "Toneladas acumuladas mes",
                                "#11439A"));
                            row.RelativeItem().Element(c => ComposeMetricCard(
                                c,
                                item.Ruta,
                                item.DieselGalonesMes.ToString("N2", CultureInfo.InvariantCulture),
                                $"Diesel acumulado mes (gal) | {item.DieselLitrosMes.ToString("N2", CultureInfo.InvariantCulture)} L",
                                "#0F766E"));
                        }
                    });
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Eficiencia operativa por ruta", body =>
                {
                    ComposeRendimientoRutaTable(body, acumuladoMes);
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Analitica ejecutiva y proyeccion", body =>
                {
                    ComposeResumenAnalitico(body, resumen);
                }));

                column.Item().Element(card => ComposeSectionCard(
                    card,
                    $"Graficos de acumulado del mes ({BuildMonthName(fechaCorte)})",
                    body =>
                    {
                        body.Column(chartColumn =>
                        {
                            chartColumn.Spacing(8);

                            chartColumn.Item().Row(row =>
                            {
                                row.Spacing(12);

                                row.RelativeItem().Element(c => ComposeDualBarChart(
                                    c,
                                    "Toneladas acumuladas del mes",
                                    acumuladoMes.Select(x => new BarRow(x.Ruta, x.ToneladasMes)).ToList(),
                                    "N1",
                                    "#11439A",
                                    "#1D4ED8"));

                                row.RelativeItem().Element(c => ComposeDualBarChart(
                                    c,
                                    "Diesel acumulado del mes (gal)",
                                    acumuladoMes.Select(x => new BarRow(x.Ruta, x.DieselGalonesMes)).ToList(),
                                    "N2",
                                    "#0F766E",
                                    "#C8A24A"));
                            });

                            chartColumn.Item().Element(c => ComposeDualBarChart(
                                c,
                                "Rendimiento T/GAL por ruta",
                                acumuladoMes.Select(x => new BarRow(x.Ruta, x.ToneladasPorGalon)).ToList(),
                                "N3",
                                "#15803D",
                                "#B45309"));
                        });
                    }));

                column.Item().Element(card => ComposeSectionCard(
                    card,
                    $"Comparativo de toneladas por mes (Enero a {BuildMonthName(fechaCorte)})",
                    body =>
                    {
                        ComposeComparativoMensualGraph(body, comparativoMensual);
                    }));
            });
        }

        private static void ComposeComparativoMensualGraph(
            IContainer container,
            IReadOnlyList<ComparativoMesRow> rows)
        {
            if (rows.Count == 0)
            {
                container.Text("Sin datos para generar comparativo mensual.")
                    .FontColor(Colors.Grey.Darken1);
                return;
            }

            var maxValue = rows
                .SelectMany(r => new[] { r.TritonToneladas, r.PavonAsmToneladas })
                .DefaultIfEmpty(0m)
                .Max();

            container.Column(column =>
            {
                column.Spacing(5);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);
                        c.RelativeColumn(1.15f);
                        c.RelativeColumn(1.15f);
                        c.ConstantColumn(75);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Mes");
                        header.Cell().Element(HeaderCell).Text("TRITON");
                        header.Cell().Element(HeaderCell).Text("PAVON ASM");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                    });

                    foreach (var row in rows)
                    {
                        table.Cell().Element(BodyCell).Text(row.Mes);
                        table.Cell().Element(cell => BodyCell(cell).PaddingVertical(5))
                            .Element(c => ComposeBarIndicator(c, row.TritonToneladas, maxValue, "#11439A", "N1"));
                        table.Cell().Element(cell => BodyCell(cell).PaddingVertical(5))
                            .Element(c => ComposeBarIndicator(c, row.PavonAsmToneladas, maxValue, "#1D4ED8", "N1"));
                        table.Cell().Element(BodyCell).AlignRight().Text((row.TritonToneladas + row.PavonAsmToneladas).ToString("N1", CultureInfo.InvariantCulture));
                    }
                });
            });
        }

        private static void ComposeRendimientoRutaTable(IContainer container, IReadOnlyList<RutaAcumuladoRow> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.0f);
                    c.RelativeColumn(0.75f);
                    c.RelativeColumn(0.8f);
                    c.RelativeColumn(0.65f);
                    c.RelativeColumn(0.65f);
                    c.RelativeColumn(0.65f);
                    c.RelativeColumn(0.65f);
                    c.RelativeColumn(0.95f);
                    c.RelativeColumn(0.8f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Ruta");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Toneladas");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Diesel (gal)");
                    header.Cell().Element(HeaderCell).AlignRight().Text("T/GAL");
                    header.Cell().Element(HeaderCell).AlignRight().Text("L/T");
                    header.Cell().Element(HeaderCell).AlignRight().Text("% Part.");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Var. %");
                    header.Cell().Element(HeaderCell).Text("Accion");
                    header.Cell().Element(HeaderCell).AlignCenter().Text("Estado");
                });

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan(9).Element(cell =>
                        BodyCell(cell).AlignCenter().Text("Sin datos por ruta para el periodo seleccionado."));
                    return;
                }

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).Text(row.Ruta);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.ToneladasMes.ToString("N1", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.DieselGalonesMes.ToString("N2", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.ToneladasPorGalon.ToString("N3", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.LitrosPorTonelada.ToString("N3", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.ParticipacionToneladasPct.ToString("N1", CultureInfo.InvariantCulture)}%");
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.VariacionVsPromedioPct.ToString("N1", CultureInfo.InvariantCulture)}%");
                    table.Cell().Element(BodyCell).Text(row.AccionRecomendada);
                    table.Cell().Element(BodyCell).AlignCenter().Text(row.EstadoRendimiento);
                }
            });
        }

        private static void ComposeResumenAnalitico(IContainer container, ResumenAnalitico resumen)
        {
            container.Column(column =>
            {
                column.Spacing(7);

                column.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Total mes", resumen.TotalToneladasMes.ToString("N1", CultureInfo.InvariantCulture), "Toneladas", "#11439A"));
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Total diesel", resumen.TotalDieselGalonesMes.ToString("N2", CultureInfo.InvariantCulture), $"Galones | {resumen.TotalDieselLitrosMes.ToString("N2", CultureInfo.InvariantCulture)} L", "#0F766E"));
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Rendimiento global", resumen.RendimientoGlobalTGal.ToString("N3", CultureInfo.InvariantCulture), "T/GAL", "#0B1D3A"));
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Intensidad global", resumen.IntensidadGlobalLT.ToString("N3", CultureInfo.InvariantCulture), "L/T", "#7C3AED"));
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.5f);
                        c.RelativeColumn(1f);
                    });

                    table.Cell().Element(BodyCell).Text("Ruta lider en toneladas");
                    table.Cell().Element(BodyCell).Text($"{resumen.RutaLiderToneladas} ({resumen.ParticipacionRutaLiderPct.ToString("N1", CultureInfo.InvariantCulture)}%)");
                    table.Cell().Element(BodyCell).Text("Ruta con menor rendimiento");
                    table.Cell().Element(BodyCell).Text($"{resumen.RutaMenorRendimiento} ({resumen.RendimientoRutaMenorTGal.ToString("N3", CultureInfo.InvariantCulture)} T/GAL)");
                    table.Cell().Element(BodyCell).Text("Variacion mensual (vs mes anterior)");
                    table.Cell().Element(BodyCell).Text($"{resumen.VariacionMensualToneladas.ToString("N1", CultureInfo.InvariantCulture)} Tn ({resumen.VariacionMensualPorcentaje.ToString("N1", CultureInfo.InvariantCulture)}%)");
                    table.Cell().Element(BodyCell).Text("Proyeccion cierre de mes");
                    table.Cell().Element(BodyCell).Text($"{resumen.ProyeccionCierreToneladas.ToString("N1", CultureInfo.InvariantCulture)} Tn | {resumen.SemaforoProyeccion}");
                    table.Cell().Element(BodyCell).Text("Brecha proyectada vs meta");
                    table.Cell().Element(BodyCell).Text($"{resumen.BrechaProyectadaToneladas.ToString("N1", CultureInfo.InvariantCulture)} Tn");
                });
            });
        }

        private static void ComposeDualBarChart(
            IContainer container,
            string title,
            IReadOnlyList<BarRow> rows,
            string valueFormat,
            string firstColor,
            string secondColor)
        {
            var maxValue = rows.Select(x => x.Value).DefaultIfEmpty(0m).Max();

            container.Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(8)
                .Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Text(title).SemiBold().FontSize(10).FontColor(Colors.Grey.Darken3);

                    for (var i = 0; i < rows.Count; i++)
                    {
                        var color = i == 0 ? firstColor : secondColor;
                        var row = rows[i];

                        column.Item().Row(line =>
                        {
                            line.ConstantItem(90).AlignMiddle().Text(row.Label).FontSize(9).SemiBold();
                            line.RelativeItem().Element(c => ComposeBarIndicator(c, row.Value, maxValue, color, valueFormat));
                        });
                    }
                });
        }

        private static void ComposeBarIndicator(
            IContainer container,
            decimal value,
            decimal maxValue,
            string color,
            string valueFormat)
        {
            const float maxWidth = 210f;
            var ratio = maxValue > 0 ? (float)Math.Clamp(value / maxValue, 0m, 1m) : 0f;
            var fillWidth = Math.Max(0f, maxWidth * ratio);

            container.Width(maxWidth)
                .Height(16)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background("#F8FAFC")
                .Layers(layers =>
                {
                    layers.PrimaryLayer().Element(baseLayer =>
                    {
                        if (fillWidth > 0.5f)
                        {
                            baseLayer
                                .Width(fillWidth)
                                .Height(16)
                                .Background(color);
                        }
                    });

                    layers.Layer()
                        .AlignMiddle()
                        .AlignRight()
                        .PaddingRight(4)
                        .Text(value.ToString(valueFormat, CultureInfo.InvariantCulture))
                        .SemiBold()
                        .FontSize(8)
                        .FontColor("#11439A");
                });
        }

        private static void ComposeMetricCard(
            IContainer container,
            string ruta,
            string value,
            string label,
            string accentColor)
        {
            container.Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background("#FAFAFA")
                .Padding(8)
                .Column(column =>
                {
                    column.Spacing(3);
                    column.Item().Text(ruta).FontSize(8).SemiBold().FontColor("#1F2937");
                    column.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                    column.Item().Text(value).FontSize(12).SemiBold().FontColor(accentColor);
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

        private static IReadOnlyList<RutaAcumuladoRow> BuildAcumuladoMesPorRuta(IEnumerable<SeguimientoToneladasRowViewModel> rowsMes)
        {
            var rows = rowsMes
                .Select(r => new
                {
                    Ruta = ResolveSitio(r),
                    Toneladas = r.Toneladas,
                    DieselGalones = r.CombustibleLitros
                })
                .GroupBy(x => x.Ruta)
                .Select(g =>
                {
                    var galones = Math.Round(g.Sum(x => x.DieselGalones), 2);
                    var toneladas = Math.Round(g.Sum(x => x.Toneladas), 1);
                    var litros = Math.Round(galones * LitrosPorGalon, 2);
                    var tgal = galones > 0m
                        ? Math.Round(toneladas / galones, 3, MidpointRounding.AwayFromZero)
                        : 0m;
                    var lt = toneladas > 0m
                        ? Math.Round(litros / toneladas, 3, MidpointRounding.AwayFromZero)
                        : 0m;

                    return new RutaAcumuladoRow(
                        g.Key,
                        toneladas,
                        galones,
                        litros,
                        tgal,
                        lt,
                        0m,
                        0m,
                        "Sin accion",
                        "OPTIMO");
                })
                .OrderByDescending(x => x.ToneladasMes)
                .ToList();

            var totalToneladas = rows.Sum(x => x.ToneladasMes);
            var totalGalones = rows.Sum(x => x.DieselGalonesMes);
            var promedioTgal = totalGalones > 0m
                ? Math.Round(totalToneladas / totalGalones, 3, MidpointRounding.AwayFromZero)
                : 0m;

            for (var i = 0; i < rows.Count; i++)
            {
                var variacion = promedioTgal > 0m
                    ? Math.Round(((rows[i].ToneladasPorGalon - promedioTgal) / promedioTgal) * 100m, 1, MidpointRounding.AwayFromZero)
                    : 0m;
                rows[i] = rows[i] with
                {
                    ParticipacionToneladasPct = totalToneladas > 0m
                        ? Math.Round((rows[i].ToneladasMes / totalToneladas) * 100m, 1, MidpointRounding.AwayFromZero)
                        : 0m,
                    VariacionVsPromedioPct = variacion,
                    AccionRecomendada = ResolveRendimientoAction(ResolveRendimientoEstado(rows[i].ToneladasPorGalon, variacion)),
                    EstadoRendimiento = ResolveRendimientoEstado(rows[i].ToneladasPorGalon, variacion)
                };
            }

            return rows;
        }

        private static string ResolveRendimientoEstado(decimal toneladasPorGalon, decimal variacionPct)
        {
            if (toneladasPorGalon <= 0m || variacionPct <= -15m)
            {
                return "CRITICO";
            }

            if (variacionPct <= -8m)
            {
                return "VIGILAR";
            }

            return "OPTIMO";
        }

        private static string ResolveRendimientoAction(string estado)
        {
            if (string.Equals(estado, "CRITICO", StringComparison.OrdinalIgnoreCase))
            {
                return "Revision inmediata";
            }

            if (string.Equals(estado, "VIGILAR", StringComparison.OrdinalIgnoreCase))
            {
                return "Monitoreo 7 dias";
            }

            return "Mantener estandar";
        }

        private static ResumenAnalitico BuildResumenAnalitico(
            IReadOnlyList<RutaAcumuladoRow> acumuladoMes,
            IReadOnlyList<ComparativoMesRow> comparativoMensual,
            DateTime fechaCorte,
            decimal metaMensualObjetivo)
        {
            var totalTon = acumuladoMes.Sum(x => x.ToneladasMes);
            var totalGal = acumuladoMes.Sum(x => x.DieselGalonesMes);
            var totalLit = acumuladoMes.Sum(x => x.DieselLitrosMes);
            var rendimientoGlobal = totalGal > 0m
                ? Math.Round(totalTon / totalGal, 3, MidpointRounding.AwayFromZero)
                : 0m;
            var intensidadGlobal = totalTon > 0m
                ? Math.Round(totalLit / totalTon, 3, MidpointRounding.AwayFromZero)
                : 0m;

            var lider = acumuladoMes.OrderByDescending(x => x.ToneladasMes).FirstOrDefault();
            var menor = acumuladoMes.OrderBy(x => x.ToneladasPorGalon).FirstOrDefault();

            var actualMesTotal = comparativoMensual.LastOrDefault()?.Total ?? 0m;
            var previoMesTotal = comparativoMensual.Count >= 2
                ? comparativoMensual[^2].Total
                : 0m;
            var varTon = Math.Round(actualMesTotal - previoMesTotal, 1, MidpointRounding.AwayFromZero);
            var varPct = previoMesTotal > 0m
                ? Math.Round((varTon / previoMesTotal) * 100m, 1, MidpointRounding.AwayFromZero)
                : (actualMesTotal > 0m ? 100m : 0m);

            var diasMes = DateTime.DaysInMonth(fechaCorte.Year, fechaCorte.Month);
            var diasTranscurridos = Math.Max(1, Math.Min(diasMes, fechaCorte.Day));
            var proyeccion = Math.Round((actualMesTotal / diasTranscurridos) * diasMes, 1, MidpointRounding.AwayFromZero);
            var metaNormalizada = Math.Max(0m, metaMensualObjetivo);
            var brecha = metaNormalizada > 0m
                ? Math.Round(proyeccion - metaNormalizada, 1, MidpointRounding.AwayFromZero)
                : 0m;
            var semaforo = metaNormalizada <= 0m
                ? "SIN META"
                : proyeccion >= metaNormalizada
                    ? "VERDE"
                    : proyeccion >= metaNormalizada * 0.9m
                        ? "AMARILLO"
                        : "ROJO";

            return new ResumenAnalitico(
                TotalToneladasMes: Math.Round(totalTon, 1, MidpointRounding.AwayFromZero),
                TotalDieselGalonesMes: Math.Round(totalGal, 2, MidpointRounding.AwayFromZero),
                TotalDieselLitrosMes: Math.Round(totalLit, 2, MidpointRounding.AwayFromZero),
                RendimientoGlobalTGal: rendimientoGlobal,
                IntensidadGlobalLT: intensidadGlobal,
                RutaLiderToneladas: lider?.Ruta ?? "Sin datos",
                ParticipacionRutaLiderPct: lider?.ParticipacionToneladasPct ?? 0m,
                RutaMenorRendimiento: menor?.Ruta ?? "Sin datos",
                RendimientoRutaMenorTGal: menor?.ToneladasPorGalon ?? 0m,
                VariacionMensualToneladas: varTon,
                VariacionMensualPorcentaje: varPct,
                ProyeccionCierreToneladas: proyeccion,
                BrechaProyectadaToneladas: brecha,
                SemaforoProyeccion: semaforo);
        }

        private static IReadOnlyList<ComparativoMesRow> BuildComparativoMensualToneladas(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rowsConFecha,
            IReadOnlyList<SeguimientoToneladasRowViewModel> fallbackRows,
            DateTime fechaCorte)
        {
            if (!rowsConFecha.Any())
            {
                return new List<ComparativoMesRow>
                {
                    new(
                        Mes: BuildMonthName(fechaCorte),
                        TritonToneladas: Math.Round(fallbackRows.Where(r => ResolveSitio(r) == "TRITON").Sum(r => r.Toneladas), 1),
                        PavonAsmToneladas: Math.Round(fallbackRows.Where(r => ResolveSitio(r) == "PAVON ASM").Sum(r => r.Toneladas), 1))
                };
            }

            var result = new List<ComparativoMesRow>();
            for (var monthNumber = 1; monthNumber <= fechaCorte.Month; monthNumber++)
            {
                var rowsMonth = rowsConFecha
                    .Where(r => r.FechaEvento!.Value.Year == fechaCorte.Year &&
                                r.FechaEvento.Value.Month == monthNumber)
                    .ToList();

                var triton = rowsMonth
                    .Where(r => ResolveSitio(r) == "TRITON")
                    .Sum(r => r.Toneladas);
                var pavon = rowsMonth
                    .Where(r => ResolveSitio(r) == "PAVON ASM")
                    .Sum(r => r.Toneladas);

                var monthDate = new DateTime(fechaCorte.Year, monthNumber, 1);
                result.Add(new ComparativoMesRow(
                    Mes: BuildMonthName(monthDate),
                    TritonToneladas: Math.Round(triton, 1),
                    PavonAsmToneladas: Math.Round(pavon, 1)));
            }

            return result;
        }

        private static string BuildMonthName(DateTime date)
        {
            var es = CultureInfo.GetCultureInfo("es-ES");
            var monthName = es.DateTimeFormat.GetMonthName(date.Month);
            if (string.IsNullOrWhiteSpace(monthName))
            {
                monthName = date.ToString("MMMM", es);
            }

            return string.Concat(char.ToUpper(monthName[0], es), monthName[1..]);
        }

        private static string ResolveSitio(SeguimientoToneladasRowViewModel row)
        {
            var procedencia = NormalizeProcedencia(row.Procedencia);
            if (string.IsNullOrWhiteSpace(procedencia))
            {
                procedencia = NormalizeProcedencia(ResolveFromRuta(row.Ruta));
            }

            if (procedencia.Contains("TRITON", StringComparison.OrdinalIgnoreCase))
            {
                return "TRITON";
            }

            if (procedencia.Contains("PAVON ASM", StringComparison.OrdinalIgnoreCase) ||
                procedencia.Contains("PAVONASM", StringComparison.OrdinalIgnoreCase) ||
                procedencia.Contains("PAVON", StringComparison.OrdinalIgnoreCase))
            {
                return "PAVON ASM";
            }

            return "OTROS";
        }

        private static string ResolveFromRuta(string? ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return string.Empty;
            }

            var separatorIndex = ruta.IndexOf(" - ", StringComparison.Ordinal);
            return separatorIndex > 0 ? ruta[..separatorIndex] : ruta;
        }

        private static string NormalizeProcedencia(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var tokens = value
                .Trim()
                .ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", tokens);
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

        private sealed record RutaAcumuladoRow(
            string Ruta,
            decimal ToneladasMes,
            decimal DieselGalonesMes,
            decimal DieselLitrosMes,
            decimal ToneladasPorGalon,
            decimal LitrosPorTonelada,
            decimal ParticipacionToneladasPct,
            decimal VariacionVsPromedioPct,
            string AccionRecomendada,
            string EstadoRendimiento);

        private sealed record ComparativoMesRow(
            string Mes,
            decimal TritonToneladas,
            decimal PavonAsmToneladas)
        {
            public decimal Total => TritonToneladas + PavonAsmToneladas;
        }

        private sealed record ResumenAnalitico(
            decimal TotalToneladasMes,
            decimal TotalDieselGalonesMes,
            decimal TotalDieselLitrosMes,
            decimal RendimientoGlobalTGal,
            decimal IntensidadGlobalLT,
            string RutaLiderToneladas,
            decimal ParticipacionRutaLiderPct,
            string RutaMenorRendimiento,
            decimal RendimientoRutaMenorTGal,
            decimal VariacionMensualToneladas,
            decimal VariacionMensualPorcentaje,
            decimal ProyeccionCierreToneladas,
            decimal BrechaProyectadaToneladas,
            string SemaforoProyeccion);

        private sealed record BarRow(string Label, decimal Value);
    }
}



