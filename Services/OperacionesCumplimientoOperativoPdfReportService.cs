using System.Globalization;
using System.Text;
using ITServiceDeskApp.ViewModels.Operaciones;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class OperacionesCumplimientoOperativoPdfReportService
    {
        public static byte[] GenerateReportePdf(
            SeguimientoToneladasViewModel model,
            string webRootPath,
            decimal metaDiariaObjetivo,
            decimal metaMensualObjetivo,
            IReadOnlyDictionary<int, decimal>? metasMensualesByPeriodo = null)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var generatedAt = DateTime.Now;
            var logo = TryLoadLogo(webRootPath);
            var rowsConFecha = model.Rows.Where(r => r.FechaEvento.HasValue).ToList();
            var fechaCorte = model.FechaOperativa
                ?? (rowsConFecha.Any() ? rowsConFecha.Max(r => r.FechaEvento!.Value.Date) : DateTime.Now.Date);
            var metaDiaria = metaDiariaObjetivo > 0m
                ? metaDiariaObjetivo
                : model.MetaDiariaObjetivo > 0m
                    ? model.MetaDiariaObjetivo
                    : 0m;

            var diaActual = BuildMetric("Dia actual", fechaCorte, fechaCorte, rowsConFecha, metaDiaria);
            var diaAnterior = BuildMetric("Dia anterior", fechaCorte.AddDays(-1), fechaCorte.AddDays(-1), rowsConFecha, metaDiaria);

            var semanaActualInicio = StartOfWeek(fechaCorte, DayOfWeek.Monday);
            var semanaAnteriorInicio = semanaActualInicio.AddDays(-7);
            var metaSemanal = Math.Round(metaDiaria * 7m, 1, MidpointRounding.AwayFromZero);
            var semanaActual = BuildMetric("Semana actual", semanaActualInicio, semanaActualInicio.AddDays(6), rowsConFecha, metaSemanal);
            var semanaAnterior = BuildMetric("Semana anterior", semanaAnteriorInicio, semanaAnteriorInicio.AddDays(6), rowsConFecha, metaSemanal);

            var mesActualInicio = new DateTime(fechaCorte.Year, fechaCorte.Month, 1);
            var mesAnteriorInicio = mesActualInicio.AddMonths(-1);
            var metaMensualActual = ResolveMetaMensual(
                mesActualInicio,
                metaMensualObjetivo,
                metaDiaria,
                metasMensualesByPeriodo);
            var metaMensualAnterior = ResolveMetaMensual(
                mesAnteriorInicio,
                metaMensualObjetivo,
                metaDiaria,
                metasMensualesByPeriodo);
            var mesActual = BuildMetric("Mes actual", mesActualInicio, mesActualInicio.AddMonths(1).AddDays(-1), rowsConFecha, metaMensualActual);
            var mesAnterior = BuildMetric("Mes anterior", mesAnteriorInicio, mesAnteriorInicio.AddMonths(1).AddDays(-1), rowsConFecha, metaMensualAnterior);

            var predSemanaTon = Math.Round(Math.Max(0m, semanaActual.Toneladas + (semanaActual.Toneladas - semanaAnterior.Toneladas)), 1);
            var predSemanaDiesel = Math.Round(Math.Max(0m, semanaActual.DieselGal + (semanaActual.DieselGal - semanaAnterior.DieselGal)), 2);
            var predSemanaCumpl = CalculateCompliance(predSemanaTon, metaSemanal);

            var nextMonth = mesActualInicio.AddMonths(1);
            var predMesTon = Math.Round(Math.Max(0m, mesActual.Toneladas + (mesActual.Toneladas - mesAnterior.Toneladas)), 1);
            var predMesDiesel = Math.Round(Math.Max(0m, mesActual.DieselGal + (mesActual.DieselGal - mesAnterior.DieselGal)), 2);
            var metaPrediccionMes = ResolveMetaMensual(
                nextMonth,
                metaMensualObjetivo,
                metaDiaria,
                metasMensualesByPeriodo);
            var predMesCumpl = CalculateCompliance(predMesTon, metaPrediccionMes);

            var serieDiaria = BuildDailySeries(rowsConFecha, fechaCorte, 14);
            var comparativoMensual = BuildMonthlyComparative(rowsConFecha, fechaCorte, metaDiaria, metaMensualObjetivo, metasMensualesByPeriodo);
            var mesActualFin = mesActualInicio.AddMonths(1).AddDays(-1);
            var rowsMesActual = rowsConFecha
                .Where(r => r.FechaEvento!.Value.Date >= mesActualInicio.Date && r.FechaEvento.Value.Date <= mesActualFin.Date)
                .ToList();
            var panel = BuildDecisionPanelData(rowsMesActual, fechaCorte, predMesTon, predMesCumpl, metaMensualActual);
            var visualRows = BuildVisualProgressRows(diaActual, semanaActual, mesActual, predMesCumpl);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(h => ComposeHeader(h, generatedAt, logo, model, fechaCorte));
                    page.Content().Element(c =>
                    {
                        c.PaddingTop(10).Column(col =>
                        {
                            col.Spacing(10);

                            col.Item().Element(card => ComposeSectionCard(card, "Resumen Ejecutivo", body =>
                            {
                                body.Row(row =>
                                {
                                    row.Spacing(8);
                                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Dia actual (Tn)", diaActual.Toneladas.ToString("N1", CultureInfo.InvariantCulture), $"{diaActual.CumplimientoPct:N1}%"));
                                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Dia anterior (Tn)", diaAnterior.Toneladas.ToString("N1", CultureInfo.InvariantCulture), $"{diaAnterior.CumplimientoPct:N1}%"));
                                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Semana actual (Tn)", semanaActual.Toneladas.ToString("N1", CultureInfo.InvariantCulture), $"{semanaActual.CumplimientoPct:N1}%"));
                                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Semana anterior (Tn)", semanaAnterior.Toneladas.ToString("N1", CultureInfo.InvariantCulture), $"{semanaAnterior.CumplimientoPct:N1}%"));
                                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Mes actual (Tn)", mesActual.Toneladas.ToString("N1", CultureInfo.InvariantCulture), $"{mesActual.CumplimientoPct:N1}%"));
                                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Mes anterior (Tn)", mesAnterior.Toneladas.ToString("N1", CultureInfo.InvariantCulture), $"{mesAnterior.CumplimientoPct:N1}%"));
                                });
                            }));

                            col.Item().Element(card => ComposeSectionCard(card, "Comparativos y proyecciones", body =>
                            {
                                ComposeComparativeTable(
                                    body,
                                    diaAnterior,
                                    diaActual,
                                    semanaAnterior,
                                    semanaActual,
                                    mesAnterior,
                                    mesActual,
                                    predSemanaTon,
                                    predSemanaDiesel,
                                    predSemanaCumpl,
                                    predMesTon,
                                    predMesDiesel,
                                    predMesCumpl,
                                    metaDiaria,
                                    metaMensualActual);
                            }));

                            col.Item().Element(card => ComposeSectionCard(card, "Tablero ejecutivo visual", body =>
                            {
                                ComposeVisualDashboard(body, panel, visualRows);
                            }));

                            col.Item().Element(card => ComposeSectionCard(card, "Panel de decisiones (mes actual)", body =>
                            {
                                ComposeDecisionPanel(body, panel);
                            }));

                            col.Item().PageBreak();
                            col.Item().Element(card => ComposeSectionCard(card, "Graficos separados por periodo", body =>
                            {
                                ComposeSeparatedPeriodCharts(
                                    body,
                                    diaAnterior.Toneladas,
                                    diaActual.Toneladas,
                                    semanaAnterior.Toneladas,
                                    semanaActual.Toneladas,
                                    predSemanaTon,
                                    mesAnterior.Toneladas,
                                    mesActual.Toneladas,
                                    predMesTon);
                            }));

                            col.Item().PageBreak();
                            col.Item().Element(card => ComposeSectionCard(card, "Grafico de lineas - Tendencia diaria (14 dias)", body =>
                            {
                                ComposeLineStyleTable(body, serieDiaria, metaDiaria);
                            }));

                            col.Item().ShowEntire().Element(card => ComposeSectionCard(card, "Comparativo de toneladas por mes (Enero a la fecha)", body =>
                            {
                                body.Column(monthCol =>
                                {
                                    monthCol.Spacing(8);
                                    monthCol.Item().Svg(BuildMonthlyCombinedSvg(comparativoMensual));
                                    monthCol.Item().Element(tbl => ComposeMonthlyTable(tbl, comparativoMensual));
                                });
                            }));
                        });
                    });
                    page.Footer().Element(f => ComposeFooter(f, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeComparativeTable(
            IContainer container,
            PeriodMetric diaAnterior,
            PeriodMetric diaActual,
            PeriodMetric semanaAnterior,
            PeriodMetric semanaActual,
            PeriodMetric mesAnterior,
            PeriodMetric mesActual,
            decimal predSemanaTon,
            decimal predSemanaDiesel,
            decimal predSemanaCumpl,
            decimal predMesTon,
            decimal predMesDiesel,
            decimal predMesCumpl,
            decimal metaDiariaObjetivo,
            decimal metaMensualObjetivo)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(0.9f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Periodo");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Meta Tn");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Toneladas");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Diesel (gal)");
                    header.Cell().Element(HeaderCell).AlignRight().Text("% Cumpl.");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Delta Tn");
                    header.Cell().Element(HeaderCell).AlignCenter().Text("Semaforo");
                });

                AddRow(table, diaAnterior.Name, diaAnterior.Toneladas, diaAnterior.DieselGal, diaAnterior.CumplimientoPct, null, metaDiariaObjetivo, metaMensualObjetivo);
                AddRow(table, diaActual.Name, diaActual.Toneladas, diaActual.DieselGal, diaActual.CumplimientoPct, diaActual.Toneladas - diaAnterior.Toneladas, metaDiariaObjetivo, metaMensualObjetivo);
                AddRow(table, semanaAnterior.Name, semanaAnterior.Toneladas, semanaAnterior.DieselGal, semanaAnterior.CumplimientoPct, null, metaDiariaObjetivo, metaMensualObjetivo);
                AddRow(table, semanaActual.Name, semanaActual.Toneladas, semanaActual.DieselGal, semanaActual.CumplimientoPct, semanaActual.Toneladas - semanaAnterior.Toneladas, metaDiariaObjetivo, metaMensualObjetivo);
                AddRow(table, "Prediccion prox. semana", predSemanaTon, predSemanaDiesel, predSemanaCumpl, predSemanaTon - semanaActual.Toneladas, metaDiariaObjetivo, metaMensualObjetivo);
                AddRow(table, mesAnterior.Name, mesAnterior.Toneladas, mesAnterior.DieselGal, mesAnterior.CumplimientoPct, null, metaDiariaObjetivo, metaMensualObjetivo);
                AddRow(table, mesActual.Name, mesActual.Toneladas, mesActual.DieselGal, mesActual.CumplimientoPct, mesActual.Toneladas - mesAnterior.Toneladas, metaDiariaObjetivo, metaMensualObjetivo);
                AddRow(table, "Prediccion prox. mes", predMesTon, predMesDiesel, predMesCumpl, predMesTon - mesActual.Toneladas, metaDiariaObjetivo, metaMensualObjetivo);
            });
        }

        private static void ComposeLineStyleTable(IContainer container, IReadOnlyList<SeriesPoint> serie, decimal metaDiariaObjetivo)
        {
            container.Column(col =>
            {
                col.Spacing(6);
                col.Item().Text("Serie temporal de toneladas finalizadas por dia (linea operativa)")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken2);

                col.Item().Svg(BuildCombinedLineBarSvg(serie, metaDiariaObjetivo));
            });
        }

        private static void ComposeDecisionPanel(IContainer container, DecisionPanelData panel)
        {
            container.Column(col =>
            {
                col.Spacing(8);

                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Estado general", panel.Snapshot.EstadoGeneral, panel.Snapshot.MensajeRiesgo));
                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Cumplimiento mes", $"{panel.Snapshot.CumplimientoMesPct:N1}%", $"{panel.Snapshot.TotalTonMes:N1} Tn"));
                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Proyeccion cierre", $"{panel.Snapshot.ProyeccionMesTon:N1} Tn", $"{panel.Snapshot.BrechaProyeccionTon:+0.0;-0.0;0.0} Tn vs meta"));
                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Rendimiento global", $"{panel.Snapshot.RendimientoTGal:N3} T/GAL", $"{panel.Snapshot.IntensidadLt:N3} L/T"));
                    row.RelativeItem().Element(x => ComposeMetricCard(x, "Ratio finalizacion", $"{panel.Snapshot.RatioFinalizacionPct:N1}%", "Finalizado / (Finalizado + En ruta)"));
                });

                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(c => ComposeSiteOperationalTable(c, panel.SiteRows));
                    row.RelativeItem().Column(right =>
                    {
                        right.Spacing(8);
                        right.Item().Element(c => ComposeTopEntityTable(c, "Top 5 equipos (Tn finalizadas mes)", panel.TopEquipos));
                        right.Item().Element(c => ComposeTopEntityTable(c, "Top 5 conductores (Tn finalizadas mes)", panel.TopConductores));
                    });
                });

                col.Item().Element(c => ComposeRecommendations(c, panel));
            });
        }

        private static void ComposeVisualDashboard(
            IContainer container,
            DecisionPanelData panel,
            IReadOnlyList<VisualProgressRow> progressRows)
        {
            container.Column(col =>
            {
                col.Spacing(8);

                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(c => ComposeStatusCard(c, "Estado operativo", panel.Snapshot.EstadoGeneral, panel.Snapshot.EstadoGeneral switch
                    {
                        "ESTABLE" => "#16A34A",
                        "VIGILAR" => "#D97706",
                        _ => "#1B55B8"
                    }));
                    row.RelativeItem().Element(c => ComposeStatusCard(c, "Brecha proyectada", $"{panel.Snapshot.BrechaProyeccionTon:+0.0;-0.0;0.0} Tn", panel.Snapshot.BrechaProyeccionTon >= 0m ? "#16A34A" : "#1B55B8"));
                    row.RelativeItem().Element(c => ComposeStatusCard(c, "Sitios en alerta", $"{panel.SiteRows.Count(x => x.Estado == "CRITICO")} critico(s) | {panel.SiteRows.Count(x => x.Estado == "VIGILAR")} vigilar", "#11439A"));
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1f);
                        c.RelativeColumn(0.8f);
                        c.RelativeColumn(1.9f);
                        c.RelativeColumn(0.8f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Periodo");
                        header.Cell().Element(HeaderCell).AlignRight().Text("%");
                        header.Cell().Element(HeaderCell).Text("Avance visual");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Estado");
                    });

                    foreach (var row in progressRows)
                    {
                        table.Cell().Element(BodyCell).Text(row.Periodo);
                        table.Cell().Element(BodyCell).AlignRight().Text($"{row.CumplimientoPct:N1}%");
                        table.Cell().Element(cell => BodyCell(cell).PaddingVertical(5))
                            .Element(c => ComposeProgressBar(c, row.CumplimientoPct, row.ColorHex));
                        table.Cell().Element(BodyCell).AlignCenter().Text(row.Semaforo);
                    }
                });
            });
        }

        private static void ComposeStatusCard(IContainer container, string label, string value, string accentColor)
        {
            container.Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background("#FAFAFA")
                .Padding(8)
                .Column(col =>
                {
                    col.Spacing(2);
                    col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().Text(value).FontSize(12).SemiBold().FontColor(accentColor);
                });
        }

        private static void ComposeProgressBar(IContainer container, decimal cumplimientoPct, string colorHex)
        {
            const float barWidth = 220f;
            const float barHeight = 14f;
            var normalized = (float)Math.Clamp(cumplimientoPct / 120m, 0m, 1m);
            var fillWidth = Math.Max(1f, barWidth * normalized);

            container.Width(barWidth)
                .Height(barHeight)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background("#EEF2F7")
                .Layers(layers =>
                {
                    layers.PrimaryLayer()
                        .Width(fillWidth)
                        .Height(barHeight)
                        .Background(colorHex);
                });
        }

        private static void ComposeSiteOperationalTable(IContainer container, IReadOnlyList<SiteOperationalRow> rows)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(col =>
            {
                col.Spacing(6);
                col.Item().Text("Desempeno por sitio").SemiBold().FontSize(10).FontColor(Colors.Grey.Darken3);
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(0.8f);
                        c.RelativeColumn(0.7f);
                        c.RelativeColumn(0.7f);
                        c.RelativeColumn(0.8f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Sitio");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Tn fin.");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Tn ruta");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Diesel");
                        header.Cell().Element(HeaderCell).AlignRight().Text("T/GAL");
                        header.Cell().Element(HeaderCell).AlignRight().Text("% Part.");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Estado");
                    });

                    foreach (var row in rows)
                    {
                        table.Cell().Element(BodyCell).Text(row.Sitio);
                        table.Cell().Element(BodyCell).AlignRight().Text(row.ToneladasFinalizadas.ToString("N1", CultureInfo.InvariantCulture));
                        table.Cell().Element(BodyCell).AlignRight().Text(row.ToneladasEnRuta.ToString("N1", CultureInfo.InvariantCulture));
                        table.Cell().Element(BodyCell).AlignRight().Text(row.DieselGal.ToString("N1", CultureInfo.InvariantCulture));
                        table.Cell().Element(BodyCell).AlignRight().Text(row.ToneladasPorGalon.ToString("N3", CultureInfo.InvariantCulture));
                        table.Cell().Element(BodyCell).AlignRight().Text($"{row.ParticipacionPct:N1}%");
                        table.Cell().Element(BodyCell).AlignCenter().Text(row.Estado);
                    }
                });
            });
        }

        private static void ComposeTopEntityTable(IContainer container, string title, IReadOnlyList<TopEntityRow> rows)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(col =>
            {
                col.Spacing(5);
                col.Item().Text(title).SemiBold().FontSize(9).FontColor(Colors.Grey.Darken3);
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(20);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(0.8f);
                        c.RelativeColumn(0.7f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).AlignRight().Text("#");
                        header.Cell().Element(HeaderCell).Text("Nombre");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Tn");
                        header.Cell().Element(HeaderCell).AlignRight().Text("%");
                    });

                    if (rows.Count == 0)
                    {
                        table.Cell().ColumnSpan(4).Element(cell => BodyCell(cell).AlignCenter().Text("Sin datos."));
                        return;
                    }

                    var rank = 0;
                    foreach (var row in rows)
                    {
                        rank++;
                        table.Cell().Element(BodyCell).AlignRight().Text(rank.ToString(CultureInfo.InvariantCulture));
                        table.Cell().Element(BodyCell).Text(ShortLabel(row.Name, 24));
                        table.Cell().Element(BodyCell).AlignRight().Text(row.Toneladas.ToString("N1", CultureInfo.InvariantCulture));
                        table.Cell().Element(BodyCell).AlignRight().Text($"{row.ParticipacionPct:N1}%");
                    }
                });
            });
        }

        private static void ComposeRecommendations(IContainer container, DecisionPanelData panel)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(col =>
            {
                col.Spacing(4);
                col.Item().Text("Recomendaciones operativas").SemiBold().FontSize(10).FontColor(Colors.Grey.Darken3);
                col.Item().PaddingLeft(4).Column(list =>
                {
                    list.Spacing(2);

                    list.Item().Text($"- Prioridad general: {panel.Snapshot.EstadoGeneral}. {panel.Snapshot.MensajeRiesgo}").FontSize(9);

                    var criticos = panel.SiteRows.Where(x => string.Equals(x.Estado, "CRITICO", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (criticos.Any())
                    {
                        foreach (var c in criticos)
                        {
                            list.Item().Text($"- {c.Sitio}: {c.Accion}. Rendimiento {c.ToneladasPorGalon:N3} T/GAL.").FontSize(9);
                        }
                    }
                    else
                    {
                        list.Item().Text("- No hay sitios criticos en el periodo; mantener disciplina operativa.").FontSize(9);
                    }

                    if (panel.Snapshot.BrechaProyeccionTon < 0)
                    {
                        list.Item().Text($"- Brecha proyectada negativa de {Math.Abs(panel.Snapshot.BrechaProyeccionTon):N1} Tn: aumentar cadencia de finalizacion en turnos de mayor demanda.").FontSize(9);
                    }
                    else
                    {
                        list.Item().Text("- Proyeccion por encima de meta: sostener ritmo y controlar sobreconsumo de diesel.").FontSize(9);
                    }
                });
            });
        }

        private static void ComposeSeparatedPeriodCharts(
            IContainer container,
            decimal diaAnteriorTon,
            decimal diaActualTon,
            decimal semanaAnteriorTon,
            decimal semanaActualTon,
            decimal predSemanaTon,
            decimal mesAnteriorTon,
            decimal mesActualTon,
            decimal predMesTon)
        {
            container.Row(row =>
            {
                row.Spacing(8);

                row.RelativeItem().Element(c => ComposeMiniPeriodCard(
                    c,
                    "Dia",
                    new List<BarPoint>
                    {
                        new("Anterior", diaAnteriorTon, "#64748B"),
                        new("Actual", diaActualTon, "#11439A")
                    }));

                row.RelativeItem().Element(c => ComposeMiniPeriodCard(
                    c,
                    "Semana",
                    new List<BarPoint>
                    {
                        new("Anterior", semanaAnteriorTon, "#A16207"),
                        new("Actual", semanaActualTon, "#1D4ED8"),
                        new("Pred.", predSemanaTon, "#0F766E")
                    }));

                row.RelativeItem().Element(c => ComposeMiniPeriodCard(
                    c,
                    "Mes",
                    new List<BarPoint>
                    {
                        new("Anterior", mesAnteriorTon, "#7C3AED"),
                        new("Actual", mesActualTon, "#11439A"),
                        new("Pred.", predMesTon, "#0F766E")
                    }));
            });
        }

        private static void ComposeMiniPeriodCard(IContainer container, string title, IReadOnlyList<BarPoint> points)
        {
            container.Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(6)
                .Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Text(title).SemiBold().FontSize(9).FontColor(Colors.Grey.Darken3);
                    col.Item().Svg(BuildMiniCombinedSvg(points));
                });
        }

        private static string BuildMiniCombinedSvg(IReadOnlyList<BarPoint> points)
        {
            const int width = 238;
            const int height = 150;
            const int left = 28;
            const int right = 8;
            const int top = 10;
            const int bottom = 24;

            if (points.Count == 0)
            {
                return $"""
<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'>
  <rect x='0' y='0' width='{width}' height='{height}' fill='#ffffff'/>
</svg>
""";
            }

            var max = points.Max(x => x.Value);
            if (max <= 0) max = 1;

            var plotWidth = width - left - right;
            var plotHeight = height - top - bottom;
            var slot = plotWidth / (double)points.Count;
            var barW = Math.Max(8, slot * 0.5);
            var baseY = top + plotHeight;
            double Y(decimal v) => top + plotHeight - ((double)v / (double)max) * plotHeight;

            var poly = new StringBuilder();
            for (var i = 0; i < points.Count; i++)
            {
                var x = left + i * slot + slot / 2;
                var y = Y(points[i].Value);
                if (i > 0) poly.Append(' ');
                poly.Append(x.ToString("0.##", CultureInfo.InvariantCulture));
                poly.Append(',');
                poly.Append(y.ToString("0.##", CultureInfo.InvariantCulture));
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'>");
            sb.AppendLine($"  <rect x='0' y='0' width='{width}' height='{height}' fill='#ffffff'/>");
            sb.AppendLine($"  <line x1='{left}' y1='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' x2='{(left + plotWidth).ToString("0.##", CultureInfo.InvariantCulture)}' y2='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#94a3b8' stroke-width='1'/>");
            sb.AppendLine($"  <line x1='{left}' y1='{top}' x2='{left}' y2='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#94a3b8' stroke-width='1'/>");

            for (var i = 0; i < points.Count; i++)
            {
                var x = left + i * slot + slot / 2;
                var y = Y(points[i].Value);
                var xr = x - barW / 2;
                var h = Math.Max(0, baseY - y);
                sb.AppendLine($"  <rect x='{xr.ToString("0.##", CultureInfo.InvariantCulture)}' y='{y.ToString("0.##", CultureInfo.InvariantCulture)}' width='{barW.ToString("0.##", CultureInfo.InvariantCulture)}' height='{h.ToString("0.##", CultureInfo.InvariantCulture)}' fill='{points[i].ColorHex}' opacity='0.82' rx='2' ry='2'/>");
                sb.AppendLine($"  <text x='{x.ToString("0.##", CultureInfo.InvariantCulture)}' y='{Math.Max(top + 8, y - 6).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='middle' font-family='Arial' font-size='7' font-weight='bold' fill='#11439A'>{points[i].Value.ToString("0.0", CultureInfo.InvariantCulture)}</text>");
                sb.AppendLine($"  <text x='{x.ToString("0.##", CultureInfo.InvariantCulture)}' y='{(baseY + 11).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='middle' font-family='Arial' font-size='6.5' fill='#475569'>{points[i].Label}</text>");
            }

            sb.AppendLine($"  <polyline points='{poly}' fill='none' stroke='#1d4ed8' stroke-width='1.8'/>");
            for (var i = 0; i < points.Count; i++)
            {
                var x = left + i * slot + slot / 2;
                var y = Y(points[i].Value);
                sb.AppendLine($"  <circle cx='{x.ToString("0.##", CultureInfo.InvariantCulture)}' cy='{y.ToString("0.##", CultureInfo.InvariantCulture)}' r='2.2' fill='#1d4ed8'/>");
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static string BuildCombinedLineBarSvg(IReadOnlyList<SeriesPoint> serie, decimal metaDiariaObjetivo)
        {
            const int width = 372;
            const int height = 198;
            const int left = 42;
            const int right = 12;
            const int top = 16;
            const int bottom = 34;

            if (serie.Count == 0)
            {
                return $"""
<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'>
  <rect x='0' y='0' width='{width}' height='{height}' fill='#ffffff'/>
  <text x='16' y='26' font-family='Arial' font-size='12' fill='#334155'>Sin datos de tendencia diaria.</text>
</svg>
""";
            }

            var maxData = serie.Max(x => x.Value);
            var maxValue = Math.Max(maxData, metaDiariaObjetivo);
            if (maxValue <= 0) maxValue = 1;

            var plotWidth = width - left - right;
            var plotHeight = height - top - bottom;
            var slot = plotWidth / (double)serie.Count;
            var barWidth = Math.Max(6, slot * 0.58);

            double Y(decimal value) => top + plotHeight - ((double)value / (double)maxValue) * plotHeight;
            var baseY = top + plotHeight;

            var points = new StringBuilder();
            for (var i = 0; i < serie.Count; i++)
            {
                var xCenter = left + (i * slot) + (slot / 2);
                var y = Y(serie[i].Value);
                if (i > 0) points.Append(' ');
                points.Append(xCenter.ToString("0.##", CultureInfo.InvariantCulture));
                points.Append(',');
                points.Append(y.ToString("0.##", CultureInfo.InvariantCulture));
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'>");
            sb.AppendLine($"  <rect x='0' y='0' width='{width}' height='{height}' fill='#ffffff'/>");

            for (var g = 0; g <= 4; g++)
            {
                var yGrid = top + (plotHeight * g / 4.0);
                sb.AppendLine($"  <line x1='{left}' y1='{yGrid.ToString("0.##", CultureInfo.InvariantCulture)}' x2='{(left + plotWidth).ToString("0.##", CultureInfo.InvariantCulture)}' y2='{yGrid.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#e5e7eb' stroke-width='1'/>");
            }

            if (metaDiariaObjetivo > 0m)
            {
                var metaY = Y(metaDiariaObjetivo);
                sb.AppendLine($"  <line x1='{left}' y1='{metaY.ToString("0.##", CultureInfo.InvariantCulture)}' x2='{(left + plotWidth).ToString("0.##", CultureInfo.InvariantCulture)}' y2='{metaY.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#11439A' stroke-width='1.4' stroke-dasharray='5,4'/>");
            }

            for (var i = 0; i < serie.Count; i++)
            {
                var xCenter = left + (i * slot) + (slot / 2);
                var value = serie[i].Value;
                var y = Y(value);
                var rectX = xCenter - (barWidth / 2);
                var rectH = Math.Max(0, baseY - y);
                sb.AppendLine($"  <rect x='{rectX.ToString("0.##", CultureInfo.InvariantCulture)}' y='{y.ToString("0.##", CultureInfo.InvariantCulture)}' width='{barWidth.ToString("0.##", CultureInfo.InvariantCulture)}' height='{rectH.ToString("0.##", CultureInfo.InvariantCulture)}' fill='#c8a24a' opacity='0.78' rx='2' ry='2'/>");
            }

            sb.AppendLine($"  <polyline points='{points}' fill='none' stroke='#1d4ed8' stroke-width='2.4'/>");

            for (var i = 0; i < serie.Count; i++)
            {
                var xCenter = left + (i * slot) + (slot / 2);
                var y = Y(serie[i].Value);
                var valueLabelY = Math.Max(top + 8, y - 7);
                sb.AppendLine($"  <circle cx='{xCenter.ToString("0.##", CultureInfo.InvariantCulture)}' cy='{y.ToString("0.##", CultureInfo.InvariantCulture)}' r='2.8' fill='#1d4ed8'/>");
                sb.AppendLine($"  <text x='{xCenter.ToString("0.##", CultureInfo.InvariantCulture)}' y='{valueLabelY.ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='middle' font-family='Arial' font-size='8' font-weight='bold' fill='#11439A'>{serie[i].Value.ToString("0.0", CultureInfo.InvariantCulture)}</text>");
                sb.AppendLine($"  <text x='{xCenter.ToString("0.##", CultureInfo.InvariantCulture)}' y='{(baseY + 12).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='middle' font-family='Arial' font-size='7' fill='#475569'>{serie[i].Label}</text>");
            }

            sb.AppendLine($"  <line x1='{left}' y1='{top}' x2='{left}' y2='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#94a3b8' stroke-width='1'/>");
            sb.AppendLine($"  <line x1='{left}' y1='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' x2='{(left + plotWidth).ToString("0.##", CultureInfo.InvariantCulture)}' y2='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#94a3b8' stroke-width='1'/>");
            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static void ComposeBars(IContainer container, IReadOnlyList<BarPoint> points)
        {
            container.Svg(BuildBarsCombinedSvg(points));
        }

        private static string BuildBarsCombinedSvg(IReadOnlyList<BarPoint> points)
        {
            const int width = 372;
            const int height = 198;
            const int left = 42;
            const int right = 12;
            const int top = 16;
            const int bottom = 34;

            if (points.Count == 0)
            {
                return $"""
<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'>
  <rect x='0' y='0' width='{width}' height='{height}' fill='#ffffff'/>
  <text x='18' y='24' font-family='Arial' font-size='12' fill='#334155'>Sin datos.</text>
</svg>
""";
            }

            var max = points.Max(x => x.Value);
            if (max <= 0) max = 1;

            var plotWidth = width - left - right;
            var plotHeight = height - top - bottom;
            var slot = plotWidth / (double)points.Count;
            var barW = Math.Max(8, slot * 0.58);
            var baseY = top + plotHeight;

            double Y(decimal value) => top + plotHeight - ((double)value / (double)max) * plotHeight;

            var linePts = new StringBuilder();
            for (var i = 0; i < points.Count; i++)
            {
                var x = left + i * slot + slot / 2;
                var y = Y(points[i].Value);
                if (i > 0) linePts.Append(' ');
                linePts.Append(x.ToString("0.##", CultureInfo.InvariantCulture));
                linePts.Append(',');
                linePts.Append(y.ToString("0.##", CultureInfo.InvariantCulture));
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'>");
            sb.AppendLine($"  <rect x='0' y='0' width='{width}' height='{height}' fill='#ffffff'/>");

            for (var g = 0; g <= 4; g++)
            {
                var yGrid = top + (plotHeight * g / 4.0);
                var tickVal = max - (max * g / 4m);
                sb.AppendLine($"  <line x1='{left}' y1='{yGrid.ToString("0.##", CultureInfo.InvariantCulture)}' x2='{(left + plotWidth).ToString("0.##", CultureInfo.InvariantCulture)}' y2='{yGrid.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#e5e7eb' stroke-width='1'/>");
                sb.AppendLine($"  <text x='{(left - 4).ToString("0.##", CultureInfo.InvariantCulture)}' y='{(yGrid + 3).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='end' font-family='Arial' font-size='7' fill='#64748b'>{tickVal.ToString("0.0", CultureInfo.InvariantCulture)}</text>");
            }

            for (var i = 0; i < points.Count; i++)
            {
                var xCenter = left + i * slot + slot / 2;
                var y = Y(points[i].Value);
                var xRect = xCenter - barW / 2;
                var h = Math.Max(0, baseY - y);

                sb.AppendLine($"  <rect x='{xRect.ToString("0.##", CultureInfo.InvariantCulture)}' y='{y.ToString("0.##", CultureInfo.InvariantCulture)}' width='{barW.ToString("0.##", CultureInfo.InvariantCulture)}' height='{h.ToString("0.##", CultureInfo.InvariantCulture)}' fill='{points[i].ColorHex}' opacity='0.82' rx='2' ry='2'/>");
                sb.AppendLine($"  <text x='{xCenter.ToString("0.##", CultureInfo.InvariantCulture)}' y='{Math.Max(top + 8, y - 6).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='middle' font-family='Arial' font-size='8' font-weight='bold' fill='#11439A'>{points[i].Value.ToString("0.0", CultureInfo.InvariantCulture)}</text>");
                sb.AppendLine($"  <text x='{xCenter.ToString("0.##", CultureInfo.InvariantCulture)}' y='{(baseY + 12).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='middle' font-family='Arial' font-size='7' fill='#475569'>{points[i].Label}</text>");
            }

            sb.AppendLine($"  <polyline points='{linePts}' fill='none' stroke='#1d4ed8' stroke-width='2.1'/>");
            for (var i = 0; i < points.Count; i++)
            {
                var xCenter = left + i * slot + slot / 2;
                var y = Y(points[i].Value);
                sb.AppendLine($"  <circle cx='{xCenter.ToString("0.##", CultureInfo.InvariantCulture)}' cy='{y.ToString("0.##", CultureInfo.InvariantCulture)}' r='2.6' fill='#1d4ed8'/>");
            }

            sb.AppendLine($"  <line x1='{left}' y1='{top}' x2='{left}' y2='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#94a3b8' stroke-width='1'/>");
            sb.AppendLine($"  <line x1='{left}' y1='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' x2='{(left + plotWidth).ToString("0.##", CultureInfo.InvariantCulture)}' y2='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#94a3b8' stroke-width='1'/>");
            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static string BuildMonthlyCombinedSvg(IReadOnlyList<MonthlyRow> rows)
        {
            const int width = 760;
            const int height = 210;
            const int left = 40;
            const int right = 12;
            const int top = 16;
            const int bottom = 36;

            if (rows.Count == 0)
            {
                return $"""
<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'>
  <rect x='0' y='0' width='{width}' height='{height}' fill='#ffffff'/>
  <text x='16' y='26' font-family='Arial' font-size='12' fill='#334155'>Sin datos mensuales.</text>
</svg>
""";
            }

            var max = rows.SelectMany(r => new[] { r.Triton, r.PavonAsm, r.Total }).Max();
            if (max <= 0) max = 1;

            var plotWidth = width - left - right;
            var plotHeight = height - top - bottom;
            var groupW = plotWidth / (double)rows.Count;
            var barW = Math.Max(8, groupW * 0.24);
            var baseY = top + plotHeight;

            double Y(decimal value) => top + plotHeight - ((double)value / (double)max) * plotHeight;

            var totalLine = new StringBuilder();
            for (var i = 0; i < rows.Count; i++)
            {
                var xGroup = left + i * groupW;
                var xCenter = xGroup + groupW / 2;
                var yTotal = Y(rows[i].Total);
                if (i > 0) totalLine.Append(' ');
                totalLine.Append(xCenter.ToString("0.##", CultureInfo.InvariantCulture));
                totalLine.Append(',');
                totalLine.Append(yTotal.ToString("0.##", CultureInfo.InvariantCulture));
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'>");
            sb.AppendLine($"  <rect x='0' y='0' width='{width}' height='{height}' fill='#ffffff'/>");

            for (var g = 0; g <= 4; g++)
            {
                var yGrid = top + (plotHeight * g / 4.0);
                var tickVal = max - (max * g / 4m);
                sb.AppendLine($"  <line x1='{left}' y1='{yGrid.ToString("0.##", CultureInfo.InvariantCulture)}' x2='{(left + plotWidth).ToString("0.##", CultureInfo.InvariantCulture)}' y2='{yGrid.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#e5e7eb' stroke-width='1'/>");
                sb.AppendLine($"  <text x='{(left - 4).ToString("0.##", CultureInfo.InvariantCulture)}' y='{(yGrid + 3).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='end' font-family='Arial' font-size='7' fill='#64748b'>{tickVal.ToString("0.0", CultureInfo.InvariantCulture)}</text>");
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var xGroup = left + i * groupW;
                var x1 = xGroup + groupW * 0.25 - barW / 2;
                var x2 = xGroup + groupW * 0.60 - barW / 2;
                var xCenter = xGroup + groupW / 2;

                var yTriton = Y(rows[i].Triton);
                var yPavon = Y(rows[i].PavonAsm);
                var yTotal = Y(rows[i].Total);

                sb.AppendLine($"  <rect x='{x1.ToString("0.##", CultureInfo.InvariantCulture)}' y='{yTriton.ToString("0.##", CultureInfo.InvariantCulture)}' width='{barW.ToString("0.##", CultureInfo.InvariantCulture)}' height='{Math.Max(0, baseY - yTriton).ToString("0.##", CultureInfo.InvariantCulture)}' fill='#11439A' opacity='0.84' rx='2' ry='2'/>");
                sb.AppendLine($"  <rect x='{x2.ToString("0.##", CultureInfo.InvariantCulture)}' y='{yPavon.ToString("0.##", CultureInfo.InvariantCulture)}' width='{barW.ToString("0.##", CultureInfo.InvariantCulture)}' height='{Math.Max(0, baseY - yPavon).ToString("0.##", CultureInfo.InvariantCulture)}' fill='#1d4ed8' opacity='0.84' rx='2' ry='2'/>");
                sb.AppendLine($"  <text x='{(x1 + barW / 2).ToString("0.##", CultureInfo.InvariantCulture)}' y='{Math.Max(top + 8, yTriton - 6).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='middle' font-family='Arial' font-size='7' fill='#11439A'>{rows[i].Triton.ToString("0.0", CultureInfo.InvariantCulture)}</text>");
                sb.AppendLine($"  <text x='{(x2 + barW / 2).ToString("0.##", CultureInfo.InvariantCulture)}' y='{Math.Max(top + 8, yPavon - 6).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='middle' font-family='Arial' font-size='7' fill='#11439A'>{rows[i].PavonAsm.ToString("0.0", CultureInfo.InvariantCulture)}</text>");
                sb.AppendLine($"  <text x='{xCenter.ToString("0.##", CultureInfo.InvariantCulture)}' y='{(baseY + 12).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='middle' font-family='Arial' font-size='7' fill='#475569'>{rows[i].Label}</text>");

                sb.AppendLine($"  <circle cx='{xCenter.ToString("0.##", CultureInfo.InvariantCulture)}' cy='{yTotal.ToString("0.##", CultureInfo.InvariantCulture)}' r='2.5' fill='#0f766e'/>");
                sb.AppendLine($"  <text x='{xCenter.ToString("0.##", CultureInfo.InvariantCulture)}' y='{Math.Max(top + 8, yTotal - 8).ToString("0.##", CultureInfo.InvariantCulture)}' text-anchor='middle' font-family='Arial' font-size='7' font-weight='bold' fill='#0f766e'>{rows[i].Total.ToString("0.0", CultureInfo.InvariantCulture)}</text>");
            }

            sb.AppendLine($"  <polyline points='{totalLine}' fill='none' stroke='#0f766e' stroke-width='2.1'/>");
            sb.AppendLine($"  <line x1='{left}' y1='{top}' x2='{left}' y2='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#94a3b8' stroke-width='1'/>");
            sb.AppendLine($"  <line x1='{left}' y1='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' x2='{(left + plotWidth).ToString("0.##", CultureInfo.InvariantCulture)}' y2='{baseY.ToString("0.##", CultureInfo.InvariantCulture)}' stroke='#94a3b8' stroke-width='1'/>");
            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static void ComposeMonthlyTable(IContainer container, IReadOnlyList<MonthlyRow> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(0.8f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Mes");
                    header.Cell().Element(HeaderCell).AlignRight().Text("TRITON");
                    header.Cell().Element(HeaderCell).AlignRight().Text("PAVON ASM");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                    header.Cell().Element(HeaderCell).AlignRight().Text("% Meta");
                });

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).Text(row.Label);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.Triton.ToString("N1", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.PavonAsm.ToString("N1", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.Total.ToString("N1", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text($"{row.CumplimientoPct:N1}%");
                }
            });
        }

        private static void AddRow(
            TableDescriptor table,
            string periodo,
            decimal t,
            decimal d,
            decimal c,
            decimal? delta,
            decimal metaDiariaObjetivo,
            decimal metaMensualObjetivo)
        {
            var meta = ResolveMetaFromPeriodo(periodo, metaDiariaObjetivo, metaMensualObjetivo);
            table.Cell().Element(BodyCell).Text(periodo);
            table.Cell().Element(BodyCell).AlignRight().Text(meta.ToString("N1", CultureInfo.InvariantCulture));
            table.Cell().Element(BodyCell).AlignRight().Text(t.ToString("N1", CultureInfo.InvariantCulture));
            table.Cell().Element(BodyCell).AlignRight().Text(d.ToString("N2", CultureInfo.InvariantCulture));
            table.Cell().Element(BodyCell).AlignRight().Text($"{c:N1}%");
            table.Cell().Element(BodyCell).AlignRight().Text(delta.HasValue ? $"{(delta >= 0 ? "+" : "")}{delta.Value:N1}" : "-");
            table.Cell().Element(BodyCell).AlignCenter().Text(ResolveCumplimientoSemaforo(c));
        }

        private static decimal ResolveMetaFromPeriodo(
            string periodo,
            decimal metaDiariaObjetivo,
            decimal metaMensualObjetivo)
        {
            if (periodo.Contains("semana", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Round(metaDiariaObjetivo * 7m, 1, MidpointRounding.AwayFromZero);
            }

            if (periodo.Contains("mes", StringComparison.OrdinalIgnoreCase))
            {
                return metaMensualObjetivo;
            }

            return metaDiariaObjetivo;
        }

        private static string ResolveCumplimientoSemaforo(decimal cumplimientoPct)
        {
            if (cumplimientoPct >= 100m)
            {
                return "VERDE";
            }

            if (cumplimientoPct >= 90m)
            {
                return "AMARILLO";
            }

            return "ROJO";
        }

        private static void ComposeHeader(IContainer container, DateTime generatedAt, byte[]? logo, SeguimientoToneladasViewModel model, DateTime fechaCorte)
        {
            container.BorderBottom(2).BorderColor("#11439A").PaddingBottom(8).Row(row =>
            {
                row.ConstantItem(90).Height(58).AlignMiddle().AlignCenter().Element(box =>
                {
                    if (logo != null) box.Image(logo).FitArea();
                    else box.Text("SIN LOGO").FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                row.RelativeItem().PaddingLeft(10).Column(column =>
                {
                    column.Item().Text("TRAWZACONS").FontSize(18).SemiBold().FontColor("#11439A");
                    column.Item().Text("Operaciones - Reporte de Cumplimiento Operativo").FontSize(12).SemiBold().FontColor(Colors.Grey.Darken3);
                    column.Item().PaddingTop(2).Text($"Fuente: {model.SourceFileName ?? "Seguimiento de toneladas"}").FontSize(9).FontColor(Colors.Grey.Darken2);
                    column.Item().Text($"Corte operativo: {fechaCorte:dd/MM/yyyy}").FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                row.ConstantItem(210).AlignRight().Column(column =>
                {
                    column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                    column.Item().Text("CUMPLIMIENTO OPERATIVO").SemiBold().FontSize(10);
                    column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }

        private static void ComposeFooter(IContainer container, DateTime generatedAt)
        {
            container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text($"Confidencial | {generatedAt:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                row.ConstantItem(120).AlignRight().Text(text =>
                {
                    text.Span("Pagina ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.CurrentPageNumber().FontSize(8).SemiBold();
                    text.Span(" de ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.TotalPages().FontSize(8).SemiBold();
                });
            });
        }

        private static void ComposeSectionCard(IContainer container, string title, Action<IContainer> body)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.White).Padding(10).Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.ConstantItem(4).Height(16).Background("#11439A");
                    r.RelativeItem().PaddingLeft(8).Text(title).SemiBold().FontSize(12).FontColor(Colors.Grey.Darken4);
                });
                col.Item().PaddingTop(8).Element(body);
            });
        }

        private static void ComposeMetricCard(IContainer container, string label, string value, string sub)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Background("#FAFAFA").Padding(8).Column(col =>
            {
                col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                col.Item().Text(value).FontSize(12).SemiBold().FontColor("#0B1D3A");
                col.Item().Text(sub).FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }

        private static IContainer HeaderCell(IContainer container) =>
            container.BorderBottom(1).BorderColor("#11439A").PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor("#11439A").FontSize(9));

        private static IContainer BodyCell(IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.FontSize(9));

        private static PeriodMetric BuildMetric(
            string name,
            DateTime start,
            DateTime end,
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            decimal metaObjetivo)
        {
            var periodRows = rows.Where(r => r.FechaEvento!.Value.Date >= start.Date && r.FechaEvento.Value.Date <= end.Date).ToList();
            var toneladas = periodRows.Where(r => IsFinalizado(r.Estado)).Sum(r => r.Toneladas);
            var diesel = periodRows.Sum(r => r.CombustibleLitros);
            var compl = CalculateCompliance(toneladas, metaObjetivo);
            return new PeriodMetric(name, Math.Round(toneladas, 1), Math.Round(diesel, 2), Math.Round(compl, 1));
        }

        private static IReadOnlyList<SeriesPoint> BuildDailySeries(IReadOnlyList<SeguimientoToneladasRowViewModel> rows, DateTime endDate, int days)
        {
            var startDate = endDate.AddDays(-(days - 1));
            var result = new List<SeriesPoint>();
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                var total = rows.Where(r => r.FechaEvento!.Value.Date == d && IsFinalizado(r.Estado)).Sum(r => r.Toneladas);
                result.Add(new SeriesPoint(d.ToString("dd/MM", CultureInfo.InvariantCulture), Math.Round(total, 1)));
            }
            return result;
        }

        private static IReadOnlyList<MonthlyRow> BuildMonthlyComparative(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            DateTime cutDate,
            decimal metaDiariaObjetivo,
            decimal metaMensualObjetivo,
            IReadOnlyDictionary<int, decimal>? metasMensualesByPeriodo)
        {
            var es = CultureInfo.GetCultureInfo("es-ES");
            var data = new List<MonthlyRow>();
            for (var month = 1; month <= cutDate.Month; month++)
            {
                var monthRows = rows.Where(r => r.FechaEvento!.Value.Year == cutDate.Year && r.FechaEvento.Value.Month == month && IsFinalizado(r.Estado)).ToList();
                var triton = monthRows.Where(IsSitioTriton).Sum(r => r.Toneladas);
                var pavon = monthRows.Where(IsSitioPavonAsm).Sum(r => r.Toneladas);
                var total = triton + pavon;
                var monthDate = new DateTime(cutDate.Year, month, 1);
                var meta = ResolveMetaMensual(monthDate, metaMensualObjetivo, metaDiariaObjetivo, metasMensualesByPeriodo);
                var monthName = es.DateTimeFormat.GetMonthName(month);
                if (!string.IsNullOrWhiteSpace(monthName))
                    monthName = char.ToUpper(monthName[0], es) + monthName[1..];

                data.Add(new MonthlyRow(monthName, Math.Round(triton, 1), Math.Round(pavon, 1), Math.Round(total, 1), Math.Round(CalculateCompliance(total, meta), 1)));
            }
            return data;
        }

        private static DecisionPanelData BuildDecisionPanelData(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rowsMesActual,
            DateTime fechaCorte,
            decimal predMesTon,
            decimal predMesCumpl,
            decimal metaMensualObjetivo)
        {
            var rowsFinalizadas = rowsMesActual.Where(r => IsFinalizado(r.Estado)).ToList();
            var totalTonMes = rowsFinalizadas.Sum(r => r.Toneladas);
            var totalTonEnRuta = rowsMesActual.Where(r => IsEnRuta(r.Estado)).Sum(r => r.Toneladas);
            var totalGalMes = rowsMesActual.Sum(r => r.CombustibleLitros);
            var totalLitMes = totalGalMes * 3.78541m;
            var totalOperativas = totalTonMes + totalTonEnRuta;
            var ratioFinalizacion = totalOperativas > 0m ? (totalTonMes / totalOperativas) * 100m : 0m;

            var cumplimientoMes = Math.Round(predMesCumpl, 1, MidpointRounding.AwayFromZero);
            var metaMes = metaMensualObjetivo;
            var brecha = Math.Round(predMesTon - metaMes, 1, MidpointRounding.AwayFromZero);
            var rendimiento = totalGalMes > 0m ? Math.Round(totalTonMes / totalGalMes, 3, MidpointRounding.AwayFromZero) : 0m;
            var intensidad = totalTonMes > 0m ? Math.Round(totalLitMes / totalTonMes, 3, MidpointRounding.AwayFromZero) : 0m;

            var estado = cumplimientoMes >= 100m && ratioFinalizacion >= 85m ? "ESTABLE" :
                cumplimientoMes >= 90m && ratioFinalizacion >= 75m ? "VIGILAR" : "CRITICO";
            var mensaje = estado switch
            {
                "ESTABLE" => "Cumplimiento controlado y flujo de cierre saludable.",
                "VIGILAR" => "Riesgo moderado: monitorear cierre y rendimiento por sitio.",
                _ => "Riesgo alto de incumplimiento: ejecutar plan correctivo inmediato."
            };

            var siteRows = BuildSiteOperationalRows(rowsMesActual, totalTonMes);
            var topEquipos = BuildTopEntityRows(rowsFinalizadas, r => r.Equipo, totalTonMes);
            var topConductores = BuildTopEntityRows(rowsFinalizadas, r => r.Conductor, totalTonMes);

            return new DecisionPanelData(
                new DecisionSnapshot(
                    TotalTonMes: Math.Round(totalTonMes, 1, MidpointRounding.AwayFromZero),
                    CumplimientoMesPct: cumplimientoMes,
                    ProyeccionMesTon: Math.Round(predMesTon, 1, MidpointRounding.AwayFromZero),
                    BrechaProyeccionTon: brecha,
                    RendimientoTGal: rendimiento,
                    IntensidadLt: intensidad,
                    RatioFinalizacionPct: Math.Round(ratioFinalizacion, 1, MidpointRounding.AwayFromZero),
                    EstadoGeneral: estado,
                    MensajeRiesgo: mensaje),
                SiteRows: siteRows,
                TopEquipos: topEquipos,
                TopConductores: topConductores);
        }

        private static IReadOnlyList<VisualProgressRow> BuildVisualProgressRows(
            PeriodMetric diaActual,
            PeriodMetric semanaActual,
            PeriodMetric mesActual,
            decimal predMesCumpl)
        {
            return new List<VisualProgressRow>
            {
                new(
                    "Dia actual",
                    diaActual.CumplimientoPct,
                    ResolveCumplimientoSemaforo(diaActual.CumplimientoPct),
                    ResolveSemaforoColor(diaActual.CumplimientoPct)),
                new(
                    "Semana actual",
                    semanaActual.CumplimientoPct,
                    ResolveCumplimientoSemaforo(semanaActual.CumplimientoPct),
                    ResolveSemaforoColor(semanaActual.CumplimientoPct)),
                new(
                    "Mes actual",
                    mesActual.CumplimientoPct,
                    ResolveCumplimientoSemaforo(mesActual.CumplimientoPct),
                    ResolveSemaforoColor(mesActual.CumplimientoPct)),
                new(
                    "Proy. cierre mes",
                    Math.Round(predMesCumpl, 1, MidpointRounding.AwayFromZero),
                    ResolveCumplimientoSemaforo(predMesCumpl),
                    ResolveSemaforoColor(predMesCumpl))
            };
        }

        private static IReadOnlyList<SiteOperationalRow> BuildSiteOperationalRows(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            decimal totalFinalizadoMes)
        {
            var grouped = rows
                .GroupBy(ResolveSite, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var finalTon = g.Where(x => IsFinalizado(x.Estado)).Sum(x => x.Toneladas);
                    var enRutaTon = g.Where(x => IsEnRuta(x.Estado)).Sum(x => x.Toneladas);
                    var gal = g.Sum(x => x.CombustibleLitros);
                    var tonGal = gal > 0m ? Math.Round(finalTon / gal, 3, MidpointRounding.AwayFromZero) : 0m;
                    var pct = totalFinalizadoMes > 0m
                        ? Math.Round((finalTon / totalFinalizadoMes) * 100m, 1, MidpointRounding.AwayFromZero)
                        : 0m;
                    var estado = ResolveSiteEstado(tonGal);

                    return new SiteOperationalRow(
                        Sitio: g.Key,
                        ToneladasFinalizadas: Math.Round(finalTon, 1, MidpointRounding.AwayFromZero),
                        ToneladasEnRuta: Math.Round(enRutaTon, 1, MidpointRounding.AwayFromZero),
                        DieselGal: Math.Round(gal, 1, MidpointRounding.AwayFromZero),
                        ToneladasPorGalon: tonGal,
                        ParticipacionPct: pct,
                        Estado: estado,
                        Accion: ResolveSiteAccion(estado));
                })
                .OrderByDescending(x => x.ToneladasFinalizadas)
                .ToList();

            return grouped;
        }

        private static IReadOnlyList<TopEntityRow> BuildTopEntityRows(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            Func<SeguimientoToneladasRowViewModel, string?> keySelector,
            decimal totalTonMes)
        {
            return rows
                .GroupBy(r => (keySelector(r) ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .Select(g =>
                {
                    var ton = g.Sum(x => x.Toneladas);
                    return new TopEntityRow(
                        Name: g.Key,
                        Toneladas: Math.Round(ton, 1, MidpointRounding.AwayFromZero),
                        ParticipacionPct: totalTonMes > 0m
                            ? Math.Round((ton / totalTonMes) * 100m, 1, MidpointRounding.AwayFromZero)
                            : 0m);
                })
                .OrderByDescending(x => x.Toneladas)
                .Take(5)
                .ToList();
        }

        private static string ResolveSiteEstado(decimal tonGal)
        {
            if (tonGal <= 0m || tonGal < 0.28m)
            {
                return "CRITICO";
            }

            if (tonGal < 0.33m)
            {
                return "VIGILAR";
            }

            return "ESTABLE";
        }

        private static string ResolveSiteAccion(string estado)
        {
            if (string.Equals(estado, "CRITICO", StringComparison.OrdinalIgnoreCase))
            {
                return "Rebalancear despacho y revisar rendimiento de ruta.";
            }

            if (string.Equals(estado, "VIGILAR", StringComparison.OrdinalIgnoreCase))
            {
                return "Monitorear durante 72h y validar causa.";
            }

            return "Mantener estandar de operacion.";
        }

        private static string ResolveSemaforoColor(decimal cumplimientoPct)
        {
            if (cumplimientoPct >= 100m)
            {
                return "#16A34A";
            }

            if (cumplimientoPct >= 90m)
            {
                return "#D97706";
            }

            return "#1B55B8";
        }

        private static bool IsEnRuta(string? estado) =>
            !string.IsNullOrWhiteSpace(estado) &&
            estado.Contains("RUTA", StringComparison.OrdinalIgnoreCase);

        private static string ShortLabel(string? value, int max)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length <= max)
            {
                return string.IsNullOrWhiteSpace(text) ? "-" : text;
            }

            return text[..Math.Max(1, max - 3)] + "...";
        }

        private static decimal ResolveMetaMensual(
            DateTime date,
            decimal metaMensualObjetivo,
            decimal metaDiariaObjetivo,
            IReadOnlyDictionary<int, decimal>? metasMensualesByPeriodo)
        {
            if (metasMensualesByPeriodo is not null &&
                metasMensualesByPeriodo.TryGetValue(BuildYearMonthKey(date), out var metaConfigurada) &&
                metaConfigurada > 0m)
            {
                return metaConfigurada;
            }

            if (metaDiariaObjetivo > 0m)
            {
                var diasMes = DateTime.DaysInMonth(date.Year, date.Month);
                return Math.Round(metaDiariaObjetivo * diasMes, 1, MidpointRounding.AwayFromZero);
            }

            return metaMensualObjetivo > 0m ? metaMensualObjetivo : 0m;
        }

        private static int BuildYearMonthKey(DateTime date) => (date.Year * 100) + date.Month;

        private static decimal CalculateCompliance(decimal toneladas, decimal meta) =>
            meta <= 0 ? 0m : (toneladas / meta) * 100m;

        private static DateTime StartOfWeek(DateTime date, DayOfWeek startDay)
        {
            var diff = (7 + (date.DayOfWeek - startDay)) % 7;
            return date.AddDays(-diff).Date;
        }

        private static bool IsFinalizado(string? estado) =>
            !string.IsNullOrWhiteSpace(estado) &&
            (estado.Contains("FINALIZAD", StringComparison.OrdinalIgnoreCase) ||
             estado.Contains("DESCARGAD", StringComparison.OrdinalIgnoreCase) ||
             estado.Contains("COMPLETAD", StringComparison.OrdinalIgnoreCase));

        private static bool IsSitioTriton(SeguimientoToneladasRowViewModel row)
        {
            var normalized = ResolveSite(row);
            return normalized.Contains("TRITON", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSitioPavonAsm(SeguimientoToneladasRowViewModel row)
        {
            var normalized = ResolveSite(row);
            return normalized.Contains("PAVON ASM", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("PAVONASM", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("PAVON", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveSite(SeguimientoToneladasRowViewModel row)
        {
            var source = !string.IsNullOrWhiteSpace(row.Procedencia)
                ? row.Procedencia
                : row.Ruta ?? string.Empty;
            var separator = source.IndexOf(" - ", StringComparison.Ordinal);
            if (separator > 0) source = source[..separator];
            return string.Join(" ", source.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
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
                if (!File.Exists(path)) continue;
                try { return File.ReadAllBytes(path); } catch { }
            }
            return null;
        }

        private sealed record PeriodMetric(string Name, decimal Toneladas, decimal DieselGal, decimal CumplimientoPct);
        private sealed record BarPoint(string Label, decimal Value, string ColorHex);
        private sealed record SeriesPoint(string Label, decimal Value);
        private sealed record MonthlyRow(string Label, decimal Triton, decimal PavonAsm, decimal Total, decimal CumplimientoPct);
        private sealed record DecisionPanelData(
            DecisionSnapshot Snapshot,
            IReadOnlyList<SiteOperationalRow> SiteRows,
            IReadOnlyList<TopEntityRow> TopEquipos,
            IReadOnlyList<TopEntityRow> TopConductores);
        private sealed record DecisionSnapshot(
            decimal TotalTonMes,
            decimal CumplimientoMesPct,
            decimal ProyeccionMesTon,
            decimal BrechaProyeccionTon,
            decimal RendimientoTGal,
            decimal IntensidadLt,
            decimal RatioFinalizacionPct,
            string EstadoGeneral,
            string MensajeRiesgo);
        private sealed record SiteOperationalRow(
            string Sitio,
            decimal ToneladasFinalizadas,
            decimal ToneladasEnRuta,
            decimal DieselGal,
            decimal ToneladasPorGalon,
            decimal ParticipacionPct,
            string Estado,
            string Accion);
        private sealed record TopEntityRow(
            string Name,
            decimal Toneladas,
            decimal ParticipacionPct);
        private sealed record VisualProgressRow(
            string Periodo,
            decimal CumplimientoPct,
            string Semaforo,
            string ColorHex);
    }
}



