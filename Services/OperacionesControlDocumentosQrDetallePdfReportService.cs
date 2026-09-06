using ITServiceDeskApp.ViewModels.Operaciones;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class OperacionesControlDocumentosQrDetallePdfReportService
    {
        public static byte[] GeneratePdf(ControlDocumentosQrViewModel model, string webRootPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var now = DateTime.Now;
            var logo = TryLoadLogo(webRootPath);
            var caps = model.Capacitaciones ?? Array.Empty<ControlDocumentosCapacitacionEstadoViewModel>();
            var totalVencidas = caps.Count(c => string.Equals(c.EstadoFiltro, "vencida", StringComparison.OrdinalIgnoreCase));
            var totalSinFecha = caps.Count(c => string.Equals(c.EstadoFiltro, "sinfecha", StringComparison.OrdinalIgnoreCase));
            var totalPorVencer = caps.Count(c => string.Equals(c.EstadoFiltro, "porvencer", StringComparison.OrdinalIgnoreCase));
            var totalVigentes = caps.Count(c => string.Equals(c.EstadoFiltro, "vigente", StringComparison.OrdinalIgnoreCase));
            var diasMinimosPorVencer = caps
                .Where(c => string.Equals(c.EstadoFiltro, "porvencer", StringComparison.OrdinalIgnoreCase) && c.DiasRestantes.HasValue)
                .Select(c => c.DiasRestantes!.Value)
                .DefaultIfEmpty()
                .Min();

            var estadoTitulo = "Puede Operar";
            var estadoDetalle = "Todas las capacitaciones estan vigentes.";
            var colorEstado = "#166534";
            if (totalVencidas > 0 || totalSinFecha > 0)
            {
                estadoTitulo = "No Puede Operar";
                estadoDetalle = $"Tiene {totalVencidas} vencida(s) y {totalSinFecha} sin fecha.";
                colorEstado = "#0B2F75";
            }
            else if (totalPorVencer > 0)
            {
                estadoTitulo = "Puede Operar";
                estadoDetalle = diasMinimosPorVencer > 0
                    ? $"Le quedan {diasMinimosPorVencer} dia(s) antes del proximo vencimiento."
                    : "Tiene capacitaciones por vencer en 30 dias o menos.";
                colorEstado = "#92400E";
            }

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(16);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Header().Element(header =>
                    {
                        header.BorderBottom(2).BorderColor("#11439A").PaddingBottom(8).Row(row =>
                        {
                            row.ConstantItem(82).Height(50).Element(box =>
                            {
                                if (logo is not null)
                                {
                                    box.Image(logo).FitArea();
                                }
                                else
                                {
                                    box.Text("SIN LOGO").FontColor(Colors.Grey.Darken1).FontSize(8);
                                }
                            });

                            row.RelativeItem().PaddingLeft(8).Column(col =>
                            {
                                col.Item().Text("Registro QR de capacitaciones")
                                    .FontSize(15)
                                    .SemiBold()
                                    .FontColor("#11439A");
                                col.Item().Text(model.Conductor)
                                    .FontSize(12)
                                    .SemiBold()
                                    .FontColor("#1E293B");
                                col.Item().Text($"Codigo: {model.ConductorKey} | Generado: {now:dd/MM/yyyy HH:mm}")
                                    .FontSize(8)
                                    .FontColor(Colors.Grey.Darken1);
                            });
                        });
                    });

                    page.Content().Element(content =>
                    {
                        content.Column(col =>
                        {
                            col.Spacing(8);
                            col.Item().Border(1).BorderColor(colorEstado).Background("#F8FAFC").Padding(8).Column(statusCol =>
                            {
                                statusCol.Item().Text(estadoTitulo).SemiBold().FontSize(14).FontColor(colorEstado);
                                statusCol.Item().PaddingTop(2).Text(estadoDetalle).FontColor(colorEstado);
                                statusCol.Item().PaddingTop(4).Text(
                                    $"Vigentes: {totalVigentes} | Por vencer: {totalPorVencer} | Vencidas: {totalVencidas} | Sin fecha: {totalSinFecha}")
                                    .FontSize(8)
                                    .FontColor("#334155");
                            });

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2.4f);
                                    c.RelativeColumn(0.9f);
                                    c.RelativeColumn(0.9f);
                                    c.RelativeColumn(2.5f);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Element(HeaderCell).Text("Capacitacion");
                                    h.Cell().Element(HeaderCell).Text("Fecha");
                                    h.Cell().Element(HeaderCell).Text("Estado");
                                    h.Cell().Element(HeaderCell).Text("Alerta");
                                });

                                foreach (var cap in caps)
                                {
                                    var estado = string.IsNullOrWhiteSpace(cap.EtiquetaSemaforo) ? "Sin fecha" : cap.EtiquetaSemaforo;
                                    var fecha = string.IsNullOrWhiteSpace(cap.Fecha) ? "-" : cap.Fecha;
                                    var alerta = string.IsNullOrWhiteSpace(cap.MensajeAlerta) ? "-" : cap.MensajeAlerta;
                                    table.Cell().Element(BodyCell).Text(cap.NombreCapacitacion);
                                    table.Cell().Element(BodyCell).Text(fecha);
                                    table.Cell().Element(BodyCell).Text(estado);
                                    table.Cell().Element(BodyCell).Text(alerta);
                                }
                            });
                        });
                    });

                    page.Footer().Element(footer =>
                    {
                        footer.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text($"Control Documentos QR | {now:dd/MM/yyyy HH:mm}")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                            row.ConstantItem(110).AlignRight().Text(txt =>
                            {
                                txt.Span("Pagina ").FontSize(8);
                                txt.CurrentPageNumber().FontSize(8).SemiBold();
                                txt.Span(" de ").FontSize(8);
                                txt.TotalPages().FontSize(8).SemiBold();
                            });
                        });
                    });
                });
            }).GeneratePdf();
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container.BorderBottom(1)
                .BorderColor("#11439A")
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor("#11439A").FontSize(8));
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container.BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.FontSize(8));
        }

        private static byte[]? TryLoadLogo(string webRootPath)
        {
            var candidates = new[]
            {
                Path.Combine(webRootPath, "images", "logo-trawzacons-corporativo.png"),
                Path.Combine(webRootPath, "images", "logo-trawzacons.png")
            };

            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    return File.ReadAllBytes(candidate);
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



