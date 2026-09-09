using System.Globalization;
using System.Text;
using ITServiceDeskApp.ViewModels.Operaciones;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class OperacionesToneladasPdfReportService
    {
        public static byte[] GenerateSeguimientoPdf(
            SeguimientoToneladasViewModel model,
            string webRootPath,
            string? range = null,
            string? from = null,
            string? to = null)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var generatedAt = DateTime.Now;
            var logo = TryLoadLogo(webRootPath);
            var allRows = model.Rows ?? new List<SeguimientoToneladasRowViewModel>();
            var cutoffDate = ResolveCutoffDate(model, allRows);
            var rangeInfo = ResolveRangeInfo(cutoffDate, range, from, to);
            var rowsRange = allRows
                .Where(r => InRange(r.FechaEvento, rangeInfo.From, rangeInfo.To))
                .ToList();

            var rowsCorte = model.FechaOperativa.HasValue
                ? allRows
                    .Where(r => r.FechaEvento.HasValue && r.FechaEvento.Value.Date == model.FechaOperativa.Value.Date)
                    .ToList()
                : allRows
                    .Where(r => r.FechaEvento.HasValue && r.FechaEvento.Value.Date == cutoffDate)
                    .ToList();
            if (!rowsCorte.Any())
            {
                rowsCorte = allRows.ToList();
            }

            var descargados = BuildDescargados(rowsCorte);
            var enRuta = BuildEnRuta(rowsCorte);
            var totalToneladasEnRuta = Math.Round(enRuta.Sum(x => x.Toneladas), 1, MidpointRounding.AwayFromZero);
            var toneladasTritonCorte = rowsCorte
                .Where(r => IsFinalizado(r.Estado) && NormalizeProcedencia(ResolveProcedencia(r)) == "TRITON")
                .Sum(r => r.Toneladas);
            var toneladasPavonAsmCorte = rowsCorte
                .Where(r => IsFinalizado(r.Estado) && NormalizeProcedencia(ResolveProcedencia(r)) == "PAVON ASM")
                .Sum(r => r.Toneladas);

            var metaDiariaTriton = model.MetaDiariaTritonObjetivo > 0m ? model.MetaDiariaTritonObjetivo : 0m;
            var metaDiariaPavon = model.MetaDiariaPavonAsmObjetivo > 0m ? model.MetaDiariaPavonAsmObjetivo : 0m;
            var executiveData = BuildExecutiveData(model, allRows, rowsRange, rangeInfo, cutoffDate, metaDiariaTriton, metaDiariaPavon);
            var webKpiSnapshot = BuildWebKpiSnapshot(model, allRows, rowsCorte, cutoffDate, metaDiariaTriton, metaDiariaPavon, executiveData.Comparison);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(header => ComposeHeader(header, generatedAt, logo, model, rangeInfo.Label));
                    page.Content().Element(content => ComposeContent(
                        content,
                        model,
                        executiveData,
                        descargados,
                        enRuta,
                        totalToneladasEnRuta,
                        toneladasTritonCorte,
                        toneladasPavonAsmCorte,
                        metaDiariaTriton,
                        metaDiariaPavon,
                        webKpiSnapshot));
                    page.Footer().Element(footer => ComposeFooter(footer, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, DateTime generatedAt, byte[]? logo, SeguimientoToneladasViewModel model, string rangeLabel)
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
                        column.Item().Text("TRAWZACONS")
                            .FontSize(18)
                            .SemiBold()
                            .FontColor("#11439A");
                        column.Item().Text("Operaciones - Reporte de Seguimiento de Toneladas")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(2).Text($"Periodo: {rangeLabel} | Corte operativo: {fechaOperativa}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(180).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("REPORTE OPERACIONES").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(
            IContainer container,
            SeguimientoToneladasViewModel model,
            ExecutiveReportData executiveData,
            IReadOnlyList<EquipoDescargadoRow> descargados,
            IReadOnlyList<EquipoEnRutaRow> enRuta,
            decimal totalToneladasEnRuta,
            decimal toneladasTritonCorte,
            decimal toneladasPavonAsmCorte,
            decimal metaDiariaTriton,
            decimal metaDiariaPavon,
            WebKpiSnapshot webKpiSnapshot)
        {
            container.PaddingTop(10).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(card => ComposeSectionCard(card, "Resumen ejecutivo operativo", body =>
                {
                    ComposeExecutiveSummary(body, executiveData);
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Comparativos operativos", body =>
                {
                    ComposeComparisonSection(body, executiveData.Range, executiveData.Comparison);
                }));

                column.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Tendencia 7 dias (toneladas finalizadas)", body =>
                    {
                        ComposeTrendTable(body, executiveData.Trend7);
                    }));
                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Proyeccion de cierre mensual", body =>
                    {
                        ComposeProjectionTable(body, executiveData.Projection);
                    }));
                });

                column.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Top 5 equipos", body =>
                    {
                        ComposeEquipmentTable(body, executiveData.TopEquipos, "Sin datos para este periodo.");
                    }));
                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Bottom 5 equipos", body =>
                    {
                        ComposeEquipmentTable(body, executiveData.BottomEquipos, "Sin datos para este periodo.");
                    }));
                });

                column.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Alertas operativas accionables", body =>
                    {
                        ComposeAlerts(body, executiveData.Alerts);
                    }));
                    row.RelativeItem().Element(card => ComposeSectionCard(card, "Calidad y trazabilidad de datos", body =>
                    {
                        ComposeQualityTable(body, executiveData.Quality);
                    }));
                });

                column.Item().PageBreak();

                column.Item().Element(card => ComposeSectionCard(card, "Panel de seguimiento (vista web)", body =>
                {
                    ComposeWebKpiSnapshot(body, webKpiSnapshot, metaDiariaTriton, metaDiariaPavon);
                }));

                column.Item().Element(card => ComposeSectionCard(card, $"Equipos descargados ({descargados.Count})", body =>
                {
                    if (descargados.Count == 0)
                    {
                        body.Text("Sin equipos descargados para el corte operativo.").FontColor(Colors.Grey.Darken1);
                        return;
                    }

                    ComposeDescargadosTable(body, descargados);
                }));

                column.Item().Element(card => ComposeSectionCard(card, $"Equipos en ruta ({enRuta.Count})", body =>
                {
                    if (enRuta.Count == 0)
                    {
                        body.Text("Sin equipos en ruta para el corte operativo.").FontColor(Colors.Grey.Darken1);
                        return;
                    }

                    ComposeEnRutaTable(body, enRuta);
                }));
            });
        }

        private static void ComposeExecutiveSummary(IContainer container, ExecutiveReportData data)
        {
            var status = ResolveStatus(data.Metrics.Cumplimiento);

            container.Column(column =>
            {
                column.Spacing(8);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Periodo activo: {data.Range.Label} | Fecha de corte: {data.Range.To:dd/MM/yyyy}")
                        .FontSize(9)
                        .FontColor("#526178");
                    row.ConstantItem(86)
                        .AlignRight()
                        .Border(1)
                        .BorderColor(status.ColorHex)
                        .Background("#F2F8FF")
                        .PaddingVertical(3)
                        .PaddingHorizontal(6)
                        .AlignMiddle()
                        .AlignCenter()
                        .Text(status.Text)
                        .FontSize(8)
                        .SemiBold()
                        .FontColor(status.ColorHex);
                });

                column.Item().Element(range => ComposeRangePills(range, data.Range.Key));

                column.Item().Row(row =>
                {
                    row.Spacing(6);
                    row.RelativeItem().Element(c =>
                        ComposeMetricCard(
                            c,
                            "Toneladas periodo",
                            $"{data.Metrics.TotalToneladas:N1} Tn",
                            "#0B1D3A",
                            $"TRITON {data.Metrics.TritonToneladas:N1} | PAVON ASM {data.Metrics.PavonToneladas:N1}"));
                    row.RelativeItem().Element(c =>
                        ComposeMetricCard(
                            c,
                            "Cumplimiento",
                            $"{data.Metrics.Cumplimiento:N1}%",
                            status.ColorHex,
                            $"Meta periodo {data.Metrics.MetaPeriodo:N1} Tn"));
                    row.RelativeItem().Element(c =>
                        ComposeMetricCard(
                            c,
                            "Brecha",
                            $"{Math.Abs(data.Metrics.Brecha):N1} Tn",
                            data.Metrics.Brecha > 0m ? "#11439A" : "#15803D",
                            data.Metrics.Brecha > 0m ? "Pendiente por cubrir" : "Meta cubierta"));
                    row.RelativeItem().Element(c =>
                        ComposeMetricCard(
                            c,
                            "Estado general",
                            status.Text,
                            status.ColorHex,
                            "Semaforo operativo consolidado"));
                    row.RelativeItem().Element(c =>
                        ComposeMetricCard(
                            c,
                            "Registros del periodo",
                            data.Metrics.Registros.ToString(CultureInfo.InvariantCulture),
                            "#334155",
                            $"Finalizados {data.Metrics.FinalizadosCount} | En ruta {data.Metrics.EnRutaCount}"));
                });
            });
        }

        private static void ComposeComparisonSection(IContainer container, DateRangeInfo range, ComparisonData comparison)
        {
            container.Row(row =>
            {
                row.Spacing(6);
                row.RelativeItem().Element(c =>
                    ComposeMetricCard(
                        c,
                        "Total vs ayer",
                        $"{FormatSigned(comparison.TotalVsAyerToneladas)} Tn ({FormatSigned(comparison.TotalVsAyerPct)}%)",
                        comparison.TotalVsAyerToneladas >= 0m ? "#15803D" : "#11439A",
                        $"TRITON {FormatSigned(comparison.TritonVsAyerToneladas)} | PAVON ASM {FormatSigned(comparison.PavonVsAyerToneladas)}"));
                row.RelativeItem().Element(c =>
                    ComposeMetricCard(
                        c,
                        "Total vs promedio 7 dias",
                        $"{FormatSigned(comparison.TotalVsPromedio7Toneladas)} Tn ({FormatSigned(comparison.TotalVsPromedio7Pct)}%)",
                        comparison.TotalVsPromedio7Toneladas >= 0m ? "#15803D" : "#11439A",
                        $"TRITON {FormatSigned(comparison.TritonVsPromedio7Toneladas)} | PAVON ASM {FormatSigned(comparison.PavonVsPromedio7Toneladas)}"));
                row.RelativeItem().Element(c =>
                    ComposeMetricCard(
                        c,
                        "Filtro activo",
                        $"Del {range.From:dd/MM} al {range.To:dd/MM}",
                        "#334155",
                        "Alertas, tendencia y KPIs responden al filtro seleccionado."));
            });
        }

        private static void ComposeWebKpiSnapshot(IContainer container, WebKpiSnapshot snapshot, decimal metaDiariaTriton, decimal metaDiariaPavon)
        {
            container.Column(column =>
            {
                column.Spacing(7);
                column.Item().Row(row =>
                {
                    row.Spacing(6);
                    row.RelativeItem().Element(c =>
                        ComposeDailyGoalCard(
                            c,
                            "Toneladas al dia TRITON",
                            snapshot.ToneladasTritonHoy,
                            metaDiariaTriton,
                            snapshot.TritonVsAyer,
                            snapshot.TritonVsPromedio7,
                            "#11439A"));
                    row.RelativeItem().Element(c =>
                        ComposeDailyGoalCard(
                            c,
                            "Toneladas al dia PAVON ASM",
                            snapshot.ToneladasPavonHoy,
                            metaDiariaPavon,
                            snapshot.PavonVsAyer,
                            snapshot.PavonVsPromedio7,
                            "#1D4ED8"));
                });

                column.Item().Row(row =>
                {
                    row.Spacing(6);
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Equipos descargados", snapshot.EquiposDescargados.ToString(CultureInfo.InvariantCulture), "#0B6E4F"));
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Equipos en ruta", snapshot.EquiposEnRuta.ToString(CultureInfo.InvariantCulture), "#D97706"));
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Toneladas en ruta", $"{snapshot.ToneladasEnRuta:N1}", "#A16207"));
                });

                column.Item().Row(row =>
                {
                    row.Spacing(6);
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Toneladas TRITON (mes)", $"{snapshot.ToneladasTritonMes:N1}", "#0F766E"));
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Toneladas PAVON ASM (mes)", $"{snapshot.ToneladasPavonMes:N1}", "#1D4ED8"));
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Toneladas promedio por viaje", $"{snapshot.PromedioPorViaje:N1}", "#2563EB"));
                    row.RelativeItem().Element(c => ComposeMetricCard(c, "Toneladas acumuladas del mes", $"{snapshot.ToneladasAcumuladasMes:N1}", "#7C3AED"));
                });
            });
        }
        private static void ComposeTrendTable(IContainer container, IReadOnlyList<DailyTotals> trend)
        {
            if (trend.Count == 0)
            {
                container.Text("Sin datos para tendencia de 7 dias.").FontColor(Colors.Grey.Darken1);
                return;
            }

            var maxReferencia = Math.Max(1m, trend.Max(x => Math.Max(x.Triton, x.Pavon)));
            var maxTotal = Math.Max(1m, trend.Max(x => x.Total));

            container.Column(column =>
            {
                column.Spacing(5);
                column.Item().Row(legend =>
                {
                    legend.Spacing(8);
                    legend.ConstantItem(92)
                        .Border(1)
                        .BorderColor("#BFD8FA")
                        .Background("#F2F8FF")
                        .PaddingVertical(2)
                        .PaddingHorizontal(6)
                        .Text("TRITON")
                        .FontSize(7.5f)
                        .SemiBold()
                        .FontColor("#11439A");
                    legend.ConstantItem(102)
                        .Border(1)
                        .BorderColor("#BFD8FA")
                        .Background("#F2F8FF")
                        .PaddingVertical(2)
                        .PaddingHorizontal(6)
                        .Text("PAVON ASM")
                        .FontSize(7.5f)
                        .SemiBold()
                        .FontColor("#1D4ED8");
                });

                foreach (var day in trend.OrderBy(x => x.Date))
                {
                    var ratio = day.Total / maxTotal;
                    var lectura = ratio >= 0.8m ? "Alto" : (ratio >= 0.45m ? "Medio" : "Bajo");
                    var lecturaColor = lectura == "Alto" ? "#15803D" : lectura == "Medio" ? "#D97706" : "#11439A";

                    column.Item()
                        .Border(1)
                        .BorderColor("#D7E3F3")
                        .Background("#FBFDFF")
                        .Padding(6)
                        .Column(dayColumn =>
                        {
                            dayColumn.Spacing(3);
                            dayColumn.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{day.Date:dd/MM}  |  Total {day.Total:N1} Tn")
                                    .FontSize(8.5f)
                                    .SemiBold()
                                    .FontColor("#0B1D3A");
                                row.ConstantItem(46).AlignRight().Text(lectura).FontSize(8).SemiBold().FontColor(lecturaColor);
                            });

                            dayColumn.Item().Element(bar => ComposeBarLine(bar, "TRITON", day.Triton, maxReferencia, "#11439A"));
                            dayColumn.Item().Element(bar => ComposeBarLine(bar, "PAVON ASM", day.Pavon, maxReferencia, "#1D4ED8"));
                        });
                }
            });
        }

        private static void ComposeProjectionTable(IContainer container, MonthlyProjectionData projection)
        {
            container.Column(column =>
            {
                column.Spacing(5);
                column.Item().Element(item => ComposeProjectionCard(item, "TRITON", projection.RealTriton, projection.EsperadaTriton, projection.MetaMensualTriton, "#11439A"));
                column.Item().Element(item => ComposeProjectionCard(item, "PAVON ASM", projection.RealPavon, projection.EsperadaPavon, projection.MetaMensualPavon, "#1D4ED8"));
                column.Item().Element(item => ComposeProjectionCard(item, "TOTAL", projection.RealTotal, projection.EsperadaTotal, projection.MetaMensualTotal, "#0B1D3A"));

                column.Item().PaddingTop(2).Text($"Dias transcurridos del mes: {projection.DiasTranscurridos}")
                    .FontSize(8)
                    .FontColor("#526178");
            });
        }

        private static void ComposeEquipmentTable(IContainer container, IReadOnlyList<EquipmentPerformanceRow> rows, string emptyMessage)
        {
            if (rows.Count == 0)
            {
                container.Text(emptyMessage).FontColor(Colors.Grey.Darken1);
                return;
            }

            var maxToneladas = Math.Max(1m, rows.Max(x => x.Toneladas));

            container.Column(column =>
            {
                column.Spacing(4);
                foreach (var row in rows)
                {
                    column.Item()
                        .Border(1)
                        .BorderColor("#D7E3F3")
                        .Background("#FBFDFF")
                        .Padding(6)
                        .Column(itemColumn =>
                        {
                            itemColumn.Item().Row(itemRow =>
                            {
                                itemRow.RelativeItem().Text(row.Equipo).SemiBold().FontSize(9).FontColor("#0B1D3A");
                                itemRow.ConstantItem(70).AlignRight().Text($"{row.Toneladas:N1} Tn").SemiBold().FontSize(8.5f).FontColor("#111827");
                                itemRow.ConstantItem(44).AlignRight().Text($"{row.Viajes} via.").FontSize(8).FontColor(Colors.Grey.Darken2);
                            });

                            itemColumn.Item().PaddingTop(3).Element(bar =>
                                ComposeBarLine(bar, "Rendimiento", row.Toneladas, maxToneladas, "#0B2F75"));
                        });
                }
            });
        }

        private static void ComposeAlerts(IContainer container, IReadOnlyList<AlertItem> alerts)
        {
            if (alerts.Count == 0)
            {
                container.Text("Sin alertas para el periodo.").FontColor(Colors.Grey.Darken1);
                return;
            }

            container.Column(column =>
            {
                column.Spacing(4);
                foreach (var alert in alerts)
                {
                    var color = alert.Level switch
                    {
                        "ALTA" => "#0B2F75",
                        "MEDIA" => "#B45309",
                        _ => "#1D4ED8"
                    };

                    column.Item().Border(1)
                        .BorderColor("#D7E3F3")
                        .Background("#FBFDFF")
                        .Padding(6)
                        .Column(item =>
                        {
                            item.Item().Text($"{alert.Level} - {alert.Message}")
                                .FontSize(8.5f)
                                .SemiBold()
                                .FontColor(color);
                            item.Item().PaddingTop(2).Text(alert.Action)
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken2);
                        });
                }
            });
        }

        private static void ComposeQualityTable(IContainer container, DataQualityData quality)
        {
            container.Column(column =>
            {
                column.Spacing(5);
                column.Item().Row(row =>
                {
                    row.Spacing(6);
                    row.RelativeItem().Element(card => ComposeMetricCard(card, "Ultima fecha en datos", quality.LatestDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), "#0B1D3A"));
                    row.RelativeItem().Element(card => ComposeMetricCard(card, "Registros cargados", quality.TotalRows.ToString(CultureInfo.InvariantCulture), "#0B1D3A"));
                });
                column.Item().Row(row =>
                {
                    row.Spacing(6);
                    row.RelativeItem().Element(card => ComposeMetricCard(card, "Registros incompletos", quality.IncompleteRows.ToString(CultureInfo.InvariantCulture), "#11439A"));
                    row.RelativeItem().Element(card => ComposeMetricCard(card, "Completitud", $"{quality.CompletenessPct:N1}%", quality.CompletenessPct >= 95m ? "#15803D" : "#D97706"));
                });
                column.Item().Element(card => ComposeMetricCard(card, "Registros sin fecha", quality.MissingDateRows.ToString(CultureInfo.InvariantCulture), "#334155"));

                column.Item().PaddingTop(2)
                    .Text($"Fuente: {quality.SourceFileName}")
                    .FontSize(8)
                    .FontColor("#526178");
            });
        }

        private static void ComposeDescargadosTable(IContainer container, IReadOnlyList<EquipoDescargadoRow> rows)
        {
            container.Column(column =>
            {
                column.Spacing(4);
                foreach (var grupo in rows.GroupBy(x => x.Procedencia).OrderBy(g => g.Key))
                {
                    column.Item().Text($"{grupo.Key} ({grupo.Count()})")
                        .FontSize(8)
                        .SemiBold()
                        .FontColor("#0B1D3A");

                    foreach (var row in grupo)
                    {
                        column.Item()
                            .Border(1)
                            .BorderColor("#D7E3F3")
                            .Background("#FBFDFF")
                            .Padding(5)
                            .Row(cardRow =>
                            {
                                cardRow.RelativeItem().Text($"{row.Equipo} - {row.Conductor}")
                                    .FontSize(8.5f)
                                    .SemiBold()
                                    .FontColor("#0B1D3A");
                                cardRow.ConstantItem(72).AlignRight().Text($"{row.Toneladas:N1} Tn")
                                    .FontSize(8)
                                    .SemiBold()
                                    .FontColor("#111827");
                                cardRow.ConstantItem(70).AlignRight().Text($"{row.Combustible:N1} L")
                                    .FontSize(8)
                                    .FontColor(Colors.Grey.Darken2);
                            });
                    }
                }
            });
        }

        private static void ComposeEnRutaTable(IContainer container, IReadOnlyList<EquipoEnRutaRow> equipos)
        {
            container.Column(column =>
            {
                column.Spacing(4);
                foreach (var grupo in equipos.GroupBy(x => x.Procedencia).OrderBy(g => g.Key))
                {
                    column.Item().Text($"{grupo.Key} ({grupo.Count()})")
                        .FontSize(8)
                        .SemiBold()
                        .FontColor("#0B1D3A");

                    foreach (var equipo in grupo)
                    {
                        column.Item()
                            .Border(1)
                            .BorderColor("#D7E3F3")
                            .Background("#FBFDFF")
                            .Padding(5)
                            .Row(cardRow =>
                            {
                                cardRow.RelativeItem().Text($"{equipo.Equipo} - {equipo.Conductor}")
                                    .FontSize(8.5f)
                                    .SemiBold()
                                    .FontColor("#0B1D3A");
                                cardRow.ConstantItem(86).AlignRight().Text($"{equipo.Toneladas:N1} Tn")
                                    .FontSize(8)
                                    .SemiBold()
                                    .FontColor("#111827");
                            });
                    }
                }
            });
        }

        private static void ComposeProjectionCard(IContainer container, string sitio, decimal real, decimal esperada, decimal metaMensual, string accentColor)
        {
            var cumplimientoEsperado = esperada > 0m ? (real / esperada) * 100m : 0m;
            var cierreMensual = metaMensual > 0m ? (real / metaMensual) * 100m : 0m;
            var esperadoColor = cumplimientoEsperado >= 100m ? "#15803D" : cumplimientoEsperado >= 85m ? "#D97706" : "#11439A";

            container.Border(1)
                .BorderColor("#D7E3F3")
                .Background("#FBFDFF")
                .Padding(6)
                .Column(column =>
                {
                    column.Spacing(3);
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(sitio).SemiBold().FontSize(9).FontColor(accentColor);
                        row.ConstantItem(86).AlignRight().Text($"Real {real:N1}").FontSize(8).SemiBold().FontColor("#111827");
                    });
                    column.Item().Text($"Esperada {esperada:N1} | Meta mes {metaMensual:N1}")
                        .FontSize(7.5f)
                        .FontColor(Colors.Grey.Darken2);
                    column.Item().Element(bar => ComposeBarLine(bar, "Cumpl. esperada", cumplimientoEsperado, 100m, esperadoColor, true));
                    column.Item().Text($"Cierre mensual: {cierreMensual:N1}%")
                        .FontSize(8)
                        .SemiBold()
                        .FontColor("#0B1D3A");
                });
        }

        private static void ComposeBarLine(IContainer container, string label, decimal value, decimal maxValue, string fillColor, bool isPercentage = false)
        {
            var safeMax = maxValue > 0m ? maxValue : 1m;
            var ratio = Math.Clamp(value / safeMax, 0m, 1m);
            var fillWidth = Math.Max(2f, (float)Math.Round(130m * ratio, 1, MidpointRounding.AwayFromZero));
            if (ratio <= 0m)
            {
                fillWidth = 0f;
            }

            container.Row(row =>
            {
                row.ConstantItem(78).AlignMiddle().Text(label).FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                row.ConstantItem(132).Height(8)
                    .Border(1)
                    .BorderColor("#D7E3F3")
                    .Background("#EEF2F7")
                    .AlignLeft()
                    .Element(fill =>
                    {
                        var width = fillWidth > 0f ? fillWidth : 0.1f;
                        var color = fillWidth > 0f ? fillColor : "#EEF2F7";
                        fill.Width(width).Height(8).Background(color);
                    });
                row.RelativeItem().AlignRight().Text(isPercentage ? $"{value:N1}%" : $"{value:N1}")
                    .FontSize(7.5f)
                    .SemiBold()
                    .FontColor("#0B1D3A");
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
                            .FontColor(Colors.Grey.Darken4);
                    });

                    column.Item().PaddingTop(8).Element(bodyComposer);
                });
        }

        private static void ComposeMetricCard(IContainer container, string label, string value, string accentColor, string? helper = null)
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
                    if (!string.IsNullOrWhiteSpace(helper))
                    {
                        column.Item().Text(helper).FontSize(7.5f).FontColor("#526178");
                    }
                });
        }

        private static void ComposeRangePills(IContainer container, string activeRangeKey)
        {
            var keys = new[] { "hoy", "ayer", "semana", "mes", "equinox", "custom" };

            container.Row(row =>
            {
                row.Spacing(4);
                foreach (var key in keys)
                {
                    var isActive = string.Equals(activeRangeKey, key, StringComparison.OrdinalIgnoreCase);
                    var label = key switch
                    {
                        "hoy" => "Hoy",
                        "ayer" => "Ayer",
                        "semana" => "Semana",
                        "mes" => "Mes",
                        "equinox" => "Equinox",
                        "custom" => "Rango",
                        _ => key
                    };

                    row.ConstantItem(58)
                        .Border(1)
                        .BorderColor(isActive ? "#1D4ED8" : "#C7D4E8")
                        .Background(isActive ? "#2563EB" : "#F1F5FB")
                        .PaddingVertical(2)
                        .AlignCenter()
                        .Text(label)
                        .FontSize(7.5f)
                        .SemiBold()
                        .FontColor(isActive ? "#FFFFFF" : "#334155");
                }
            });
        }

        private static void ComposeDailyGoalCard(
            IContainer container,
            string title,
            decimal toneladasActuales,
            decimal metaObjetivo,
            decimal vsAyer,
            decimal vsPromedio7,
            string accentColor)
        {
            var progreso = metaObjetivo > 0m
                ? Math.Round((toneladasActuales / metaObjetivo) * 100m, 1, MidpointRounding.AwayFromZero)
                : 0m;
            var progresoVisual = Math.Clamp(progreso, 0m, 100m);
            var faltante = Math.Max(0m, metaObjetivo - toneladasActuales);
            var barraColor = progreso >= 100m ? "#15803D" : progreso >= 80m ? "#D97706" : accentColor;

            container.Border(1)
                .BorderColor("#D7E3F3")
                .Background("#FFFFFF")
                .Padding(8)
                .Column(column =>
                {
                    column.Spacing(3);
                    column.Item().Text(title).FontSize(8).SemiBold().FontColor("#64748B");
                    column.Item().Text($"{toneladasActuales:N1}").FontSize(13).SemiBold().FontColor(accentColor);
                    column.Item().Height(8).Row(bar =>
                    {
                        bar.RelativeItem((float)Math.Max(0.1m, progresoVisual))
                            .Background(progresoVisual > 0m ? barraColor : "#E5EAF2");
                        bar.RelativeItem((float)Math.Max(0.1m, 100m - progresoVisual))
                            .Background("#E5EAF2");
                    });
                    column.Item().Row(meta =>
                    {
                        meta.RelativeItem().Text($"{progreso:N1}%").FontSize(7.5f).FontColor("#526178");
                        meta.RelativeItem().AlignRight().Text($"Meta {metaObjetivo:N0}").FontSize(7.5f).FontColor("#526178");
                    });
                    column.Item().Text(faltante > 0m ? $"Faltante: {faltante:N1} Tn" : "Meta cumplida")
                        .FontSize(7.5f)
                        .SemiBold()
                        .FontColor(faltante > 0m ? "#B45309" : "#15803D");
                    column.Item().Text($"Hoy vs ayer {FormatSigned(vsAyer)} Tn | vs prom. 7 dias {FormatSigned(vsPromedio7)} Tn")
                        .FontSize(7.5f)
                        .FontColor("#526178");
                });
        }

        private static ExecutiveReportData BuildExecutiveData(
            SeguimientoToneladasViewModel model,
            IReadOnlyList<SeguimientoToneladasRowViewModel> allRows,
            IReadOnlyList<SeguimientoToneladasRowViewModel> rowsRange,
            DateRangeInfo rangeInfo,
            DateTime cutoffDate,
            decimal metaDiariaTriton,
            decimal metaDiariaPavon)
        {
            var metrics = BuildExecutiveMetrics(rowsRange, rangeInfo, metaDiariaTriton, metaDiariaPavon);
            var comparison = BuildComparisons(allRows, cutoffDate);
            var trend7 = BuildTrend7(allRows, cutoffDate);
            var projection = BuildMonthlyProjection(model, allRows, cutoffDate, metaDiariaTriton, metaDiariaPavon);
            var (topEquipos, bottomEquipos) = BuildTopBottom(rowsRange);
            var alerts = BuildAlerts(metrics, rowsRange, allRows);
            var quality = BuildQuality(model, allRows, cutoffDate);

            return new ExecutiveReportData(
                Range: rangeInfo,
                Metrics: metrics,
                Comparison: comparison,
                Trend7: trend7,
                Projection: projection,
                TopEquipos: topEquipos,
                BottomEquipos: bottomEquipos,
                Alerts: alerts,
                Quality: quality);
        }

        private static ExecutiveMetrics BuildExecutiveMetrics(
            IReadOnlyList<SeguimientoToneladasRowViewModel> rowsRange,
            DateRangeInfo rangeInfo,
            decimal metaDiariaTriton,
            decimal metaDiariaPavon)
        {
            var finalizados = rowsRange.Where(r => IsFinalizado(r.Estado)).ToList();
            var enRuta = rowsRange.Where(r => IsEnRuta(r.Estado)).ToList();
            var tritonToneladas = finalizados
                .Where(r => NormalizeProcedencia(ResolveProcedencia(r)) == "TRITON")
                .Sum(r => r.Toneladas);
            var pavonToneladas = finalizados
                .Where(r => NormalizeProcedencia(ResolveProcedencia(r)) == "PAVON ASM")
                .Sum(r => r.Toneladas);
            var totalToneladas = tritonToneladas + pavonToneladas;
            var diasPeriodo = Math.Max(1, (rangeInfo.To - rangeInfo.From).Days + 1);
            var metaPeriodo = (metaDiariaTriton + metaDiariaPavon) * diasPeriodo;
            var cumplimiento = metaPeriodo > 0m ? (totalToneladas / metaPeriodo) * 100m : 0m;
            var brecha = metaPeriodo - totalToneladas;
            var tritonCumpl = metaDiariaTriton > 0m ? (tritonToneladas / (metaDiariaTriton * diasPeriodo)) * 100m : 0m;
            var pavonCumpl = metaDiariaPavon > 0m ? (pavonToneladas / (metaDiariaPavon * diasPeriodo)) * 100m : 0m;

            return new ExecutiveMetrics(
                TotalToneladas: Math.Round(totalToneladas, 1, MidpointRounding.AwayFromZero),
                TritonToneladas: Math.Round(tritonToneladas, 1, MidpointRounding.AwayFromZero),
                PavonToneladas: Math.Round(pavonToneladas, 1, MidpointRounding.AwayFromZero),
                MetaPeriodo: Math.Round(metaPeriodo, 1, MidpointRounding.AwayFromZero),
                Cumplimiento: Math.Round(cumplimiento, 1, MidpointRounding.AwayFromZero),
                Brecha: Math.Round(brecha, 1, MidpointRounding.AwayFromZero),
                Registros: rowsRange.Count,
                FinalizadosCount: finalizados.Count,
                EnRutaCount: enRuta.Count,
                TritonCumpl: Math.Round(tritonCumpl, 1, MidpointRounding.AwayFromZero),
                PavonCumpl: Math.Round(pavonCumpl, 1, MidpointRounding.AwayFromZero));
        }

        private static ComparisonData BuildComparisons(IReadOnlyList<SeguimientoToneladasRowViewModel> rows, DateTime cutoffDate)
        {
            var hoy = BuildDailyTotals(rows, cutoffDate);
            var ayer = BuildDailyTotals(rows, cutoffDate.AddDays(-1));
            var trend7 = BuildTrend7(rows, cutoffDate);
            var promedio7Total = trend7.Count > 0 ? trend7.Average(x => x.Total) : 0m;
            var promedio7Triton = trend7.Count > 0 ? trend7.Average(x => x.Triton) : 0m;
            var promedio7Pavon = trend7.Count > 0 ? trend7.Average(x => x.Pavon) : 0m;

            var totalVsAyer = hoy.Total - ayer.Total;
            var totalVsAyerPct = ayer.Total > 0m ? (totalVsAyer / ayer.Total) * 100m : 0m;
            var totalVsProm7 = hoy.Total - promedio7Total;
            var totalVsProm7Pct = promedio7Total > 0m ? (totalVsProm7 / promedio7Total) * 100m : 0m;

            return new ComparisonData(
                TotalVsAyerToneladas: Math.Round(totalVsAyer, 1, MidpointRounding.AwayFromZero),
                TotalVsAyerPct: Math.Round(totalVsAyerPct, 1, MidpointRounding.AwayFromZero),
                TotalVsPromedio7Toneladas: Math.Round(totalVsProm7, 1, MidpointRounding.AwayFromZero),
                TotalVsPromedio7Pct: Math.Round(totalVsProm7Pct, 1, MidpointRounding.AwayFromZero),
                TritonVsAyerToneladas: Math.Round(hoy.Triton - ayer.Triton, 1, MidpointRounding.AwayFromZero),
                PavonVsAyerToneladas: Math.Round(hoy.Pavon - ayer.Pavon, 1, MidpointRounding.AwayFromZero),
                TritonVsPromedio7Toneladas: Math.Round(hoy.Triton - promedio7Triton, 1, MidpointRounding.AwayFromZero),
                PavonVsPromedio7Toneladas: Math.Round(hoy.Pavon - promedio7Pavon, 1, MidpointRounding.AwayFromZero));
        }

        private static List<DailyTotals> BuildTrend7(IReadOnlyList<SeguimientoToneladasRowViewModel> rows, DateTime cutoffDate)
        {
            var trend = new List<DailyTotals>();
            for (var i = 6; i >= 0; i--)
            {
                var day = cutoffDate.AddDays(-i);
                trend.Add(BuildDailyTotals(rows, day));
            }

            return trend;
        }

        private static DailyTotals BuildDailyTotals(IReadOnlyList<SeguimientoToneladasRowViewModel> rows, DateTime date)
        {
            var dailyFinalizados = rows
                .Where(r => r.FechaEvento.HasValue && r.FechaEvento.Value.Date == date.Date && IsFinalizado(r.Estado))
                .ToList();

            var triton = dailyFinalizados
                .Where(r => NormalizeProcedencia(ResolveProcedencia(r)) == "TRITON")
                .Sum(r => r.Toneladas);
            var pavon = dailyFinalizados
                .Where(r => NormalizeProcedencia(ResolveProcedencia(r)) == "PAVON ASM")
                .Sum(r => r.Toneladas);

            return new DailyTotals(
                Date: date.Date,
                Triton: Math.Round(triton, 1, MidpointRounding.AwayFromZero),
                Pavon: Math.Round(pavon, 1, MidpointRounding.AwayFromZero));
        }

        private static MonthlyProjectionData BuildMonthlyProjection(
            SeguimientoToneladasViewModel model,
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            DateTime cutoffDate,
            decimal metaDiariaTriton,
            decimal metaDiariaPavon)
        {
            var monthRows = rows
                .Where(r =>
                    r.FechaEvento.HasValue &&
                    r.FechaEvento.Value.Year == cutoffDate.Year &&
                    r.FechaEvento.Value.Month == cutoffDate.Month &&
                    r.FechaEvento.Value.Date <= cutoffDate &&
                    IsFinalizado(r.Estado))
                .ToList();

            var realTriton = monthRows
                .Where(r => NormalizeProcedencia(ResolveProcedencia(r)) == "TRITON")
                .Sum(r => r.Toneladas);
            var realPavon = monthRows
                .Where(r => NormalizeProcedencia(ResolveProcedencia(r)) == "PAVON ASM")
                .Sum(r => r.Toneladas);
            var realTotal = realTriton + realPavon;
            var diasTranscurridos = cutoffDate.Day;
            var esperadaTriton = metaDiariaTriton * diasTranscurridos;
            var esperadaPavon = metaDiariaPavon * diasTranscurridos;
            var esperadaTotal = esperadaTriton + esperadaPavon;

            return new MonthlyProjectionData(
                RealTriton: Math.Round(realTriton, 1, MidpointRounding.AwayFromZero),
                RealPavon: Math.Round(realPavon, 1, MidpointRounding.AwayFromZero),
                RealTotal: Math.Round(realTotal, 1, MidpointRounding.AwayFromZero),
                EsperadaTriton: Math.Round(esperadaTriton, 1, MidpointRounding.AwayFromZero),
                EsperadaPavon: Math.Round(esperadaPavon, 1, MidpointRounding.AwayFromZero),
                EsperadaTotal: Math.Round(esperadaTotal, 1, MidpointRounding.AwayFromZero),
                MetaMensualTriton: Math.Round(model.MetaMensualTriton, 1, MidpointRounding.AwayFromZero),
                MetaMensualPavon: Math.Round(model.MetaMensualPavonAsm, 1, MidpointRounding.AwayFromZero),
                MetaMensualTotal: Math.Round(model.MetaMensualTotal, 1, MidpointRounding.AwayFromZero),
                DiasTranscurridos: diasTranscurridos);
        }

        private static (IReadOnlyList<EquipmentPerformanceRow> Top, IReadOnlyList<EquipmentPerformanceRow> Bottom) BuildTopBottom(IReadOnlyList<SeguimientoToneladasRowViewModel> rowsRange)
        {
            var grouped = rowsRange
                .Where(r => IsFinalizado(r.Estado))
                .GroupBy(r => (r.Equipo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .Select(g => new EquipmentPerformanceRow(
                    Equipo: g.Key,
                    Toneladas: Math.Round(g.Sum(x => x.Toneladas), 1, MidpointRounding.AwayFromZero),
                    Viajes: g.Sum(x => x.Viajes)))
                .ToList();

            var top = grouped
                .OrderByDescending(x => x.Toneladas)
                .ThenBy(x => x.Equipo)
                .Take(5)
                .ToList();
            var bottom = grouped
                .OrderBy(x => x.Toneladas)
                .ThenBy(x => x.Equipo)
                .Take(5)
                .ToList();

            return (top, bottom);
        }

        private static List<AlertItem> BuildAlerts(
            ExecutiveMetrics metrics,
            IReadOnlyList<SeguimientoToneladasRowViewModel> rowsRange,
            IReadOnlyList<SeguimientoToneladasRowViewModel> allRows)
        {
            var alerts = new List<AlertItem>();

            if (metrics.Cumplimiento < 100m)
            {
                alerts.Add(new AlertItem(
                    Level: "ALTA",
                    Message: $"Brecha de {Math.Max(0m, metrics.Brecha):N1} Tn para cumplir la meta del periodo.",
                    Action: "Accion: priorizar carga y despacho en la procedencia con mayor rezago."));
            }

            if (metrics.EnRutaCount > metrics.FinalizadosCount)
            {
                alerts.Add(new AlertItem(
                    Level: "MEDIA",
                    Message: $"Hay mas registros en ruta ({metrics.EnRutaCount}) que finalizados ({metrics.FinalizadosCount}).",
                    Action: "Accion: revisar cuellos de botella en descarga y tiempos de patio."));
            }

            var equiposSinCierre = rowsRange
                .Where(r => !string.IsNullOrWhiteSpace(r.Equipo))
                .GroupBy(r => (r.Equipo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Any(x => IsEnRuta(x.Estado)) && !g.Any(x => IsFinalizado(x.Estado)))
                .Select(g => g.Key)
                .Take(3)
                .ToList();
            if (equiposSinCierre.Any())
            {
                alerts.Add(new AlertItem(
                    Level: "MEDIA",
                    Message: $"Equipos sin cierre finalizado en el periodo: {string.Join(", ", equiposSinCierre)}.",
                    Action: "Accion: confirmar estado real y registrar descarga para mantener trazabilidad."));
            }

            if (metrics.TritonCumpl < 90m)
            {
                alerts.Add(new AlertItem(
                    Level: "MEDIA",
                    Message: $"TRITON por debajo del 90% de cumplimiento ({metrics.TritonCumpl:N1}%).",
                    Action: "Accion: redistribuir equipos hacia rutas TRITON en el siguiente turno."));
            }

            if (metrics.PavonCumpl < 90m)
            {
                alerts.Add(new AlertItem(
                    Level: "BAJA",
                    Message: $"PAVON ASM por debajo del 90% de cumplimiento ({metrics.PavonCumpl:N1}%).",
                    Action: "Accion: validar disponibilidad de equipos PAVON ASM y frecuencia de viajes."));
            }

            var incompletos = allRows.Count(IsIncomplete);
            var incompletosPct = allRows.Count > 0 ? (decimal)incompletos / allRows.Count * 100m : 0m;
            if (incompletosPct >= 5m)
            {
                alerts.Add(new AlertItem(
                    Level: "MEDIA",
                    Message: $"Calidad de datos en riesgo: {incompletosPct:N1}% de registros incompletos.",
                    Action: "Accion: completar conductor, ruta, equipo o estado antes del proximo cierre."));
            }

            if (!alerts.Any())
            {
                alerts.Add(new AlertItem(
                    Level: "BAJA",
                    Message: "Operacion estable: sin hallazgos criticos para el periodo filtrado.",
                    Action: "Accion: mantener ritmo operativo y monitoreo normal."));
            }

            return alerts.Take(6).ToList();
        }

        private static DataQualityData BuildQuality(
            SeguimientoToneladasViewModel model,
            IReadOnlyList<SeguimientoToneladasRowViewModel> rows,
            DateTime fallbackDate)
        {
            var latestDate = rows
                .Where(r => r.FechaEvento.HasValue)
                .Select(r => r.FechaEvento!.Value.Date)
                .DefaultIfEmpty(fallbackDate.Date)
                .Max();
            var totalRows = rows.Count;
            var incompleteRows = rows.Count(IsIncomplete);
            var completeRows = Math.Max(0, totalRows - incompleteRows);
            var completenessPct = totalRows > 0 ? (decimal)completeRows / totalRows * 100m : 100m;
            var missingDateRows = rows.Count(r => !r.FechaEvento.HasValue);
            var sourceName = string.IsNullOrWhiteSpace(model.SourceFileName) ? "Datos operativos" : model.SourceFileName!;

            return new DataQualityData(
                LatestDate: latestDate,
                TotalRows: totalRows,
                IncompleteRows: incompleteRows,
                CompletenessPct: Math.Round(completenessPct, 1, MidpointRounding.AwayFromZero),
                MissingDateRows: missingDateRows,
                SourceFileName: sourceName);
        }

        private static WebKpiSnapshot BuildWebKpiSnapshot(
            SeguimientoToneladasViewModel model,
            IReadOnlyList<SeguimientoToneladasRowViewModel> allRows,
            IReadOnlyList<SeguimientoToneladasRowViewModel> rowsCorte,
            DateTime cutoffDate,
            decimal metaDiariaTriton,
            decimal metaDiariaPavon,
            ComparisonData comparison)
        {
            var rowsMesAcumuladas = allRows
                .Where(r =>
                    r.FechaEvento.HasValue &&
                    r.FechaEvento.Value.Year == cutoffDate.Year &&
                    r.FechaEvento.Value.Month == cutoffDate.Month)
                .ToList();
            if (!rowsMesAcumuladas.Any())
            {
                rowsMesAcumuladas = allRows.ToList();
            }

            var toneladasTritonMes = rowsMesAcumuladas
                .Where(r => NormalizeProcedencia(ResolveProcedencia(r)) == "TRITON")
                .Sum(r => r.Toneladas);
            var toneladasPavonMes = rowsMesAcumuladas
                .Where(r => NormalizeProcedencia(ResolveProcedencia(r)) == "PAVON ASM")
                .Sum(r => r.Toneladas);
            var toneladasTritonHoy = rowsCorte
                .Where(r => IsFinalizado(r.Estado) && NormalizeProcedencia(ResolveProcedencia(r)) == "TRITON")
                .Sum(r => r.Toneladas);
            var toneladasPavonHoy = rowsCorte
                .Where(r => IsFinalizado(r.Estado) && NormalizeProcedencia(ResolveProcedencia(r)) == "PAVON ASM")
                .Sum(r => r.Toneladas);
            var cumplimientoMetaTriton = model.MetaMensualTriton > 0m
                ? (toneladasTritonMes / model.MetaMensualTriton) * 100m
                : 0m;
            var cumplimientoMetaPavon = model.MetaMensualPavonAsm > 0m
                ? (toneladasPavonMes / model.MetaMensualPavonAsm) * 100m
                : 0m;

            return new WebKpiSnapshot(
                ToneladasTritonHoy: Math.Round(toneladasTritonHoy, 1, MidpointRounding.AwayFromZero),
                ToneladasPavonHoy: Math.Round(toneladasPavonHoy, 1, MidpointRounding.AwayFromZero),
                EquiposDescargados: model.EquiposDescargados,
                EquiposEnRuta: model.EquiposEnRuta,
                ToneladasEnRuta: Math.Round(model.ToneladasEnRuta, 1, MidpointRounding.AwayFromZero),
                ToneladasTritonMes: Math.Round(toneladasTritonMes, 1, MidpointRounding.AwayFromZero),
                ToneladasPavonMes: Math.Round(toneladasPavonMes, 1, MidpointRounding.AwayFromZero),
                PromedioPorViaje: Math.Round(model.PromedioPorViaje, 1, MidpointRounding.AwayFromZero),
                ToneladasAcumuladasMes: Math.Round(model.ToneladasAcumuladasMes, 1, MidpointRounding.AwayFromZero),
                CumplimientoMetaTriton: Math.Round(cumplimientoMetaTriton, 1, MidpointRounding.AwayFromZero),
                CumplimientoMetaPavon: Math.Round(cumplimientoMetaPavon, 1, MidpointRounding.AwayFromZero),
                TritonVsAyer: comparison.TritonVsAyerToneladas,
                PavonVsAyer: comparison.PavonVsAyerToneladas,
                TritonVsPromedio7: comparison.TritonVsPromedio7Toneladas,
                PavonVsPromedio7: comparison.PavonVsPromedio7Toneladas);
        }

        private static List<EquipoDescargadoRow> BuildDescargados(IReadOnlyList<SeguimientoToneladasRowViewModel> rows)
        {
            return rows
                .Where(r => IsFinalizado(r.Estado))
                .GroupBy(r => (r.Equipo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .Select(g => new EquipoDescargadoRow(
                    Equipo: g.Key,
                    Procedencia: g.Select(x => NormalizeProcedencia(ResolveProcedencia(x)))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "OTROS",
                    Conductor: g.Select(x => (x.Conductor ?? string.Empty).Trim())
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Sin conductor",
                    Toneladas: Math.Round(g.Sum(x => x.Toneladas), 1, MidpointRounding.AwayFromZero),
                    Combustible: Math.Round(g.Sum(x => x.CombustibleLitros), 1, MidpointRounding.AwayFromZero)))
                .OrderBy(x => x.Procedencia)
                .ThenBy(x => x.Equipo)
                .ToList();
        }

        private static List<EquipoEnRutaRow> BuildEnRuta(IReadOnlyList<SeguimientoToneladasRowViewModel> rows)
        {
            return rows
                .Where(r => IsEnRuta(r.Estado))
                .GroupBy(r => (r.Equipo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .Select(g => new EquipoEnRutaRow(
                    Equipo: g.Key,
                    Procedencia: g.Select(x => NormalizeProcedencia(ResolveProcedencia(x)))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "OTROS",
                    Conductor: g.Select(x => (x.Conductor ?? string.Empty).Trim())
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Sin conductor",
                    Toneladas: Math.Round(g.Sum(GetToneladasEnRuta), 1, MidpointRounding.AwayFromZero)))
                .OrderBy(x => x.Procedencia)
                .ThenBy(x => x.Equipo)
                .ToList();
        }
        private static DateRangeInfo ResolveRangeInfo(DateTime cutoffDate, string? range, string? from, string? to)
        {
            var key = NormalizeRange(range);
            return key switch
            {
                "ayer" => new DateRangeInfo(cutoffDate.AddDays(-1), cutoffDate.AddDays(-1), "Ayer", key),
                "semana" => new DateRangeInfo(ResolveWeekStartSunday(cutoffDate), cutoffDate, "Semana", key),
                "mes" => new DateRangeInfo(new DateTime(cutoffDate.Year, cutoffDate.Month, 1), cutoffDate, "Mes", key),
                "equinox" => new DateRangeInfo(ResolveEquinoxRangeStart(cutoffDate), cutoffDate, "Corte Equinox", key),
                "custom" => ResolveCustomRangeInfo(cutoffDate, from, to),
                _ => new DateRangeInfo(cutoffDate, cutoffDate, "Hoy", "hoy")
            };
        }

        private static DateTime ResolveWeekStartSunday(DateTime cutoffDate)
        {
            var dayOffset = (int)cutoffDate.DayOfWeek;
            return cutoffDate.Date.AddDays(-dayOffset);
        }

        private static DateRangeInfo ResolveCustomRangeInfo(DateTime cutoffDate, string? from, string? to)
        {
            var hasFrom = TryParseIsoDate(from, out var parsedFrom);
            var hasTo = TryParseIsoDate(to, out var parsedTo);
            if (!hasFrom && !hasTo)
            {
                return new DateRangeInfo(cutoffDate, cutoffDate, "Hoy", "hoy");
            }

            if (!hasFrom)
            {
                parsedFrom = parsedTo;
            }
            else if (!hasTo)
            {
                parsedTo = parsedFrom;
            }

            if (parsedFrom.Date > parsedTo.Date)
            {
                var swap = parsedFrom;
                parsedFrom = parsedTo;
                parsedTo = swap;
            }

            return new DateRangeInfo(parsedFrom.Date, parsedTo.Date, "Rango personalizado", "custom");
        }

        private static DateTime ResolveEquinoxRangeStart(DateTime cutoffDate)
        {
            var baseMonth = new DateTime(cutoffDate.Year, cutoffDate.Month, 1);
            if (cutoffDate.Day < 28)
            {
                baseMonth = baseMonth.AddMonths(-1);
            }

            return new DateTime(baseMonth.Year, baseMonth.Month, 28);
        }

        private static bool TryParseIsoDate(string? value, out DateTime parsedDate)
        {
            parsedDate = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return DateTime.TryParseExact(
                value.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedDate);
        }

        private static DateTime ResolveCutoffDate(SeguimientoToneladasViewModel model, IReadOnlyList<SeguimientoToneladasRowViewModel> rows)
        {
            if (model.FechaOperativa.HasValue)
            {
                return model.FechaOperativa.Value.Date;
            }

            var latest = rows
                .Where(r => r.FechaEvento.HasValue)
                .Select(r => r.FechaEvento!.Value.Date)
                .DefaultIfEmpty(DateTime.Today)
                .Max();
            return latest;
        }

        private static bool InRange(DateTime? date, DateTime from, DateTime to)
        {
            return date.HasValue && date.Value.Date >= from.Date && date.Value.Date <= to.Date;
        }

        private static string NormalizeRange(string? range)
        {
            if (string.IsNullOrWhiteSpace(range))
            {
                return "hoy";
            }

            var normalized = range.Trim().ToLowerInvariant();
            return normalized is "hoy" or "ayer" or "semana" or "mes" or "equinox" or "custom"
                ? normalized
                : "hoy";
        }

        private static (string Text, string ColorHex) ResolveStatus(decimal cumplimiento)
        {
            if (cumplimiento >= 100m)
            {
                return ("Cumplida", "#15803D");
            }

            if (cumplimiento >= 90m)
            {
                return ("En riesgo", "#D97706");
            }

            return ("Critica", "#11439A");
        }

        private static string FormatSigned(decimal value)
        {
            var sign = value >= 0m ? "+" : "-";
            return $"{sign}{Math.Abs(value):N1}";
        }

        private static bool IsIncomplete(SeguimientoToneladasRowViewModel row)
        {
            return string.IsNullOrWhiteSpace(row.Equipo) ||
                   string.IsNullOrWhiteSpace(row.Conductor) ||
                   string.IsNullOrWhiteSpace(row.Estado) ||
                   string.IsNullOrWhiteSpace(row.Ruta);
        }

        private static bool IsFinalizado(string? estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return false;
            }

            var normalized = Normalize(estado);
            return normalized.Contains("finalizad", StringComparison.Ordinal) ||
                   normalized.Contains("descargad", StringComparison.Ordinal) ||
                   normalized.Contains("completad", StringComparison.Ordinal) ||
                   normalized.Contains("terminad", StringComparison.Ordinal) ||
                   normalized.Contains("cerrad", StringComparison.Ordinal);
        }

        private static bool IsEnRuta(string? estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return false;
            }

            var normalized = Normalize(estado);
            return normalized.Contains("enruta", StringComparison.Ordinal) ||
                   normalized.Contains("entrada", StringComparison.Ordinal) ||
                   normalized.Contains("encamino", StringComparison.Ordinal) ||
                   normalized.Contains("ruta", StringComparison.Ordinal) ||
                   normalized.Contains("transito", StringComparison.Ordinal) ||
                   normalized.Contains("proceso", StringComparison.Ordinal) ||
                   normalized.Contains("pendiente", StringComparison.Ordinal);
        }

        private static string Normalize(string value)
        {
            var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
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

        private static decimal GetToneladasEnRuta(SeguimientoToneladasRowViewModel row)
        {
            return row.PesoSugerido > 0 ? row.PesoSugerido : row.Toneladas;
        }

        private static string ResolveProcedencia(SeguimientoToneladasRowViewModel row)
        {
            if (!string.IsNullOrWhiteSpace(row.Procedencia))
            {
                return row.Procedencia;
            }

            var ruta = row.Ruta ?? string.Empty;
            var separatorIndex = ruta.IndexOf(" - ", StringComparison.Ordinal);
            return separatorIndex > 0 ? ruta[..separatorIndex] : ruta;
        }

        private static string NormalizeProcedencia(string? procedencia)
        {
            if (string.IsNullOrWhiteSpace(procedencia))
            {
                return string.Empty;
            }

            var tokens = procedencia
                .Trim()
                .ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", tokens);
        }

        private sealed record EquipoDescargadoRow(string Equipo, string Procedencia, string Conductor, decimal Toneladas, decimal Combustible);
        private sealed record EquipoEnRutaRow(string Equipo, string Procedencia, string Conductor, decimal Toneladas);
        private sealed record DateRangeInfo(DateTime From, DateTime To, string Label, string Key);
        private sealed record ExecutiveMetrics(
            decimal TotalToneladas,
            decimal TritonToneladas,
            decimal PavonToneladas,
            decimal MetaPeriodo,
            decimal Cumplimiento,
            decimal Brecha,
            int Registros,
            int FinalizadosCount,
            int EnRutaCount,
            decimal TritonCumpl,
            decimal PavonCumpl);
        private sealed record DailyTotals(DateTime Date, decimal Triton, decimal Pavon)
        {
            public decimal Total => Triton + Pavon;
        }

        private sealed record ComparisonData(
            decimal TotalVsAyerToneladas,
            decimal TotalVsAyerPct,
            decimal TotalVsPromedio7Toneladas,
            decimal TotalVsPromedio7Pct,
            decimal TritonVsAyerToneladas,
            decimal PavonVsAyerToneladas,
            decimal TritonVsPromedio7Toneladas,
            decimal PavonVsPromedio7Toneladas);
        private sealed record MonthlyProjectionData(
            decimal RealTriton,
            decimal RealPavon,
            decimal RealTotal,
            decimal EsperadaTriton,
            decimal EsperadaPavon,
            decimal EsperadaTotal,
            decimal MetaMensualTriton,
            decimal MetaMensualPavon,
            decimal MetaMensualTotal,
            int DiasTranscurridos);
        private sealed record WebKpiSnapshot(
            decimal ToneladasTritonHoy,
            decimal ToneladasPavonHoy,
            int EquiposDescargados,
            int EquiposEnRuta,
            decimal ToneladasEnRuta,
            decimal ToneladasTritonMes,
            decimal ToneladasPavonMes,
            decimal PromedioPorViaje,
            decimal ToneladasAcumuladasMes,
            decimal CumplimientoMetaTriton,
            decimal CumplimientoMetaPavon,
            decimal TritonVsAyer,
            decimal PavonVsAyer,
            decimal TritonVsPromedio7,
            decimal PavonVsPromedio7);
        private sealed record EquipmentPerformanceRow(string Equipo, decimal Toneladas, int Viajes);
        private sealed record AlertItem(string Level, string Message, string Action);
        private sealed record DataQualityData(
            DateTime LatestDate,
            int TotalRows,
            int IncompleteRows,
            decimal CompletenessPct,
            int MissingDateRows,
            string SourceFileName);
        private sealed record ExecutiveReportData(
            DateRangeInfo Range,
            ExecutiveMetrics Metrics,
            ComparisonData Comparison,
            IReadOnlyList<DailyTotals> Trend7,
            MonthlyProjectionData Projection,
            IReadOnlyList<EquipmentPerformanceRow> TopEquipos,
            IReadOnlyList<EquipmentPerformanceRow> BottomEquipos,
            IReadOnlyList<AlertItem> Alerts,
            DataQualityData Quality);
    }
}



