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
    public static class TicketPdfReportService
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".bmp"
        };

        public static byte[] GenerateTicketReportPdf(Ticket ticket, string webRootPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var now = DateTime.Now;
            var report = BuildReportData(ticket, webRootPath, now);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(header => ComposeHeader(header, report));
                    page.Content().Element(content => ComposeContent(content, report));
                    page.Footer().Element(footer => ComposeFooter(footer, report.GeneratedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, TicketReportData report)
        {
            container.BorderBottom(2)
                .BorderColor("#11439A")
                .PaddingBottom(8)
                .Row(row =>
                {
                    row.ConstantItem(90).Height(64).AlignMiddle().AlignCenter().Element(logo =>
                    {
                        if (report.LogoBytes != null)
                        {
                            logo.Image(report.LogoBytes).FitArea();
                        }
                        else
                        {
                            logo.Border(1).BorderColor(Colors.Grey.Lighten2)
                                .AlignCenter()
                                .AlignMiddle()
                                .Text("SIN LOGO")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                        }
                    });

                    row.RelativeItem().PaddingLeft(10).Column(column =>
                    {
                        column.Item().Text(string.Empty)
                            .FontSize(18)
                            .SemiBold()
                            .FontColor("#11439A");

                        column.Item().Text("Service Desk - Reporte Ejecutivo de Incidencia")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);

                        column.Item().PaddingTop(2).Text($"Ticket {report.TicketNumber}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(150).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("REPORTE DE INCIDENCIA").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {report.GeneratedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(IContainer container, TicketReportData report)
        {
            container.PaddingTop(12).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Resumen Corporativo",
                    body => ComposeExecutiveSummary(body, report)));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Datos Generales",
                    body => ComposeKeyValueTable(body, report.GeneralRows)));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Da�o Reportado",
                    body => body.Text(report.Description).FontSize(10)));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Gesti�n T�cnica",
                    body => ComposeKeyValueTable(body, report.TechnicalRows)));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Evidencias",
                    body => ComposeEvidenceSection(body, report.Evidences)));

                if (report.HistoryRows.Count > 0)
                {
                    column.Item().Element(section => ComposeSectionCard(
                        section,
                        "Historial de Cambios",
                        body => ComposeHistoryTable(body, report.HistoryRows)));
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
                        text.Span("P�gina ").FontSize(8).FontColor(Colors.Grey.Darken1);
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

        private static void ComposeExecutiveSummary(IContainer container, TicketReportData report)
        {
            container.Column(column =>
            {
                column.Spacing(8);

                column.Item().Text(report.ExecutiveSummary)
                    .FontSize(10)
                    .FontColor(Colors.Grey.Darken3);

                column.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(card => ComposeMetricCard(card, "Estado Final", report.StatusLabel, "#0B6E4F"));
                    row.RelativeItem().Element(card => ComposeMetricCard(card, "Cumplimiento SLA", report.SlaCompliance, report.SlaColor));
                    row.RelativeItem().Element(card => ComposeMetricCard(card, "Prioridad", report.PriorityLabel, "#D97706"));
                    row.RelativeItem().Element(card => ComposeMetricCard(card, "Tiempo Respuesta", report.ResolutionTimeLabel, "#2563EB"));
                });
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
                    column.Item().Text(label)
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                    column.Item().Text(value)
                        .FontSize(12)
                        .SemiBold()
                        .FontColor(accentColor);
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

        private static void ComposeEvidenceSection(IContainer container, IReadOnlyList<EvidenceData> evidences)
        {
            if (evidences.Count == 0)
            {
                container.Text("No hay evidencias adjuntas para esta incidencia.")
                    .FontColor(Colors.Grey.Darken1);
                return;
            }

            container.Column(column =>
            {
                column.Spacing(8);

                foreach (var evidence in evidences)
                {
                    column.Item().Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(8)
                        .Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Text(evidence.Title)
                                .SemiBold()
                                .FontSize(10);

                            if (evidence.ImageBytes != null)
                            {
                                card.Item()
                                    .Height(180)
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Lighten3)
                                    .Padding(3)
                                    .Image(evidence.ImageBytes)
                                    .FitArea();
                            }
                            else
                            {
                                card.Item().Text("No hay imagen disponible para esta evidencia.")
                                    .FontColor(Colors.Grey.Darken1)
                                    .FontSize(9);
                            }

                            card.Item().Text(evidence.Description)
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                        });
                }
            });
        }

        private static void ComposeHistoryTable(IContainer container, IReadOnlyList<HistoryRow> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Fecha");
                    header.Cell().Element(HeaderCell).Text("Campo");
                    header.Cell().Element(HeaderCell).Text("Valor Anterior");
                    header.Cell().Element(HeaderCell).Text("Valor Nuevo");
                    header.Cell().Element(HeaderCell).Text("Usuario");
                });

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).Text(row.ChangeDate);
                    table.Cell().Element(BodyCell).Text(row.Field);
                    table.Cell().Element(BodyCell).Text(row.OldValue);
                    table.Cell().Element(BodyCell).Text(row.NewValue);
                    table.Cell().Element(BodyCell).Text(row.ChangedBy);
                }
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
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.FontSize(9));
        }

        private static TicketReportData BuildReportData(Ticket ticket, string webRootPath, DateTime generatedAt)
        {
            var createdLocal = ticket.CreatedDate.ToLocalTime();
            var closedLocal = ticket.ClosedDate?.ToLocalTime();
            var slaLocal = ticket.SLADeadline.ToLocalTime();
            var nowLocal = generatedAt;

            var slaCompliance = CalculateSlaCompliance(ticket, nowLocal);
            var statusLabel = GetStatusLabel(ticket.Status);
            var priorityLabel = GetPriorityLabel(ticket.Priority);

            var resolutionTimeLabel = closedLocal.HasValue
                ? FormatDuration(closedLocal.Value - createdLocal)
                : FormatDuration(nowLocal - createdLocal);

            var executiveSummary = BuildExecutiveSummary(ticket, statusLabel, slaCompliance, createdLocal, closedLocal, slaLocal);

            var generalRows = new List<ReportRow>
            {
                new("Ticket", Safe(ticket.TicketNumber) ?? $"TK-{ticket.Id:D4}"),
                new("Fecha de Registro", createdLocal.ToString("dd/MM/yyyy HH:mm")),
                new("Fecha de Cierre", closedLocal.HasValue ? closedLocal.Value.ToString("dd/MM/yyyy HH:mm") : "No cerrada"),
                new("Usuario Solicitante", Safe(ticket.RequestingUser) ?? "No especificado"),
                new("Sitio / Sede", Safe(ticket.Site) ?? "No especificado"),
                new("�rea / Departamento", Safe(ticket.Department) ?? "No especificado"),
                new("Tipo de Incidencia", Safe(ticket.IncidentType) ?? "No especificado"),
                new("T�cnico Asignado", Safe(ticket.AssignedTechnician) ?? "Sin asignar"),
                new("Prioridad", priorityLabel),
                new("Estado", statusLabel),
                new("SLA L�mite", slaLocal.ToString("dd/MM/yyyy HH:mm")),
                new("Cumplimiento SLA", slaCompliance)
            };

            var technicalRows = new List<ReportRow>
            {
                new("Condici�n Inicial", Safe(ticket.InitialConditionNotes) ?? "Sin detalle"),
                new("Reparaciones Realizadas", Safe(ticket.RepairActionsPerformed) ?? "Sin detalle"),
                new("Causa Ra�z", Safe(ticket.RootCause) ?? "Sin detalle"),
                new("Pruebas T�cnicas", Safe(ticket.TechnicalTestsPerformed) ?? "Sin detalle"),
                new("Requiri� Repuesto", ticket.SparePartRequired ? "S�" : "No"),
                new("Compr� Repuesto", ticket.SparePartPurchased ? "S�" : "No"),
                new("Detalle Repuesto", Safe(ticket.SparePartDetails) ?? "No aplica"),
                new("Cambio de Componente", ticket.ComponentChanged ? "S�" : "No"),
                new("Componente Cambiado", Safe(ticket.ChangedComponentName) ?? "No aplica"),
                new("Costo Componente C$", FormatCurrency(ticket.ChangedComponentCostCordoba, "C$")),
                new("Costo Componente $", FormatCurrency(ticket.ChangedComponentCostUsd, "$")),
                new("Usuario Confirm� Soluci�n", ticket.UserConformityConfirmed ? "S�" : "No"),
                new("Observaciones", Safe(ticket.Observations) ?? "Sin observaciones"),
                new("Recomendaciones Preventivas", Safe(ticket.PreventiveRecommendations) ?? "Sin recomendaciones")
            };

            var evidences = new List<EvidenceData>
            {
                BuildEvidence("Evidencia del Usuario (Caso Reportado)", ticket.AttachmentPath, webRootPath),
                BuildEvidence("Evidencia Inicial (Equipo Recibido)", ticket.BeforeEvidencePath, webRootPath),
                BuildEvidence("Evidencia Final (Equipo Entregado)", ticket.AfterEvidencePath, webRootPath),
                BuildEvidence("Ficha T�cnica", ticket.TechnicalSheetPath, webRootPath),
                BuildEvidence("Orden de Salida", ticket.ExitOrderPath, webRootPath)
            };

            var historyRows = (ticket.HistoryEntries ?? Enumerable.Empty<TicketHistory>())
                .OrderBy(h => h.ChangeDate)
                .Select(h => new HistoryRow(
                    h.ChangeDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    Safe(h.FieldChanged) ?? "Campo",
                    Safe(h.OldValue) ?? "(vac�o)",
                    Safe(h.NewValue) ?? "(vac�o)",
                    Safe(h.ChangedBy) ?? "Sistema"))
                .ToList();

            return new TicketReportData
            {
                TicketNumber = Safe(ticket.TicketNumber) ?? $"TK-{ticket.Id:D4}",
                GeneratedAt = generatedAt,
                StatusLabel = statusLabel,
                PriorityLabel = priorityLabel,
                SlaCompliance = slaCompliance,
                SlaColor = slaCompliance == "Cumplido" ? "#0B6E4F" : (slaCompliance == "Vencido" ? "#11439A" : "#D97706"),
                ResolutionTimeLabel = resolutionTimeLabel,
                ExecutiveSummary = executiveSummary,
                Description = Safe(ticket.Description) ?? "Sin descripci�n.",
                GeneralRows = generalRows,
                TechnicalRows = technicalRows,
                Evidences = evidences,
                HistoryRows = historyRows,
                LogoBytes = TryLoadLogo(webRootPath)
            };
        }

        private static string BuildExecutiveSummary(
            Ticket ticket,
            string statusLabel,
            string slaCompliance,
            DateTime createdLocal,
            DateTime? closedLocal,
            DateTime slaLocal)
        {
            var technician = Safe(ticket.AssignedTechnician) ?? "Sin asignar";
            var closingLabel = closedLocal.HasValue
                ? $"La incidencia fue cerrada el {closedLocal.Value:dd/MM/yyyy HH:mm}."
                : "La incidencia permanece abierta al momento de generar este reporte.";

            return
                $"Incidencia {Safe(ticket.TicketNumber) ?? $"TK-{ticket.Id:D4}"} registrada el {createdLocal:dd/MM/yyyy HH:mm} " +
                $"por {Safe(ticket.RequestingUser) ?? "usuario no identificado"}. " +
                $"Estado actual: {statusLabel}. Cumplimiento SLA: {slaCompliance} (l�mite {slaLocal:dd/MM/yyyy HH:mm}). " +
                $"T�cnico responsable: {technician}. {closingLabel}";
        }

        private static EvidenceData BuildEvidence(string title, string? webPath, string webRootPath)
        {
            if (string.IsNullOrWhiteSpace(webPath))
            {
                return new EvidenceData(title, null, "No se adjunt� archivo.");
            }

            var fullPath = ResolvePhysicalPath(webRootPath, webPath);
            if (fullPath == null || !File.Exists(fullPath))
            {
                return new EvidenceData(title, null, $"Archivo no encontrado: {webPath}");
            }

            if (!ImageExtensions.Contains(Path.GetExtension(fullPath)))
            {
                return new EvidenceData(title, null, $"Archivo adjunto no es imagen: {webPath}");
            }

            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                return new EvidenceData(title, bytes, $"Archivo: {webPath}");
            }
            catch
            {
                return new EvidenceData(title, null, $"No se pudo leer el archivo: {webPath}");
            }
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
                    // Ignorar y probar siguiente candidato.
                }
            }

            return null;
        }

        private static string? ResolvePhysicalPath(string webRootPath, string webPath)
        {
            if (string.IsNullOrWhiteSpace(webRootPath) || string.IsNullOrWhiteSpace(webPath))
            {
                return null;
            }

            var relative = webPath
                .Trim()
                .TrimStart('~')
                .TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(webRootPath, relative);
        }

        private static string CalculateSlaCompliance(Ticket ticket, DateTime nowLocal)
        {
            var sla = ticket.SLADeadline.ToLocalTime();

            if (ticket.Status == TicketStatus.Closed)
            {
                if (!ticket.ClosedDate.HasValue)
                {
                    return "No definido";
                }

                return ticket.ClosedDate.Value.ToLocalTime() <= sla
                    ? "Cumplido"
                    : "Vencido";
            }

            return nowLocal <= sla ? "En curso" : "Vencido";
        }

        private static string GetPriorityLabel(PriorityLevel priority)
        {
            return priority switch
            {
                PriorityLevel.Low => "Baja",
                PriorityLevel.Medium => "Media",
                PriorityLevel.High => "Alta",
                PriorityLevel.Critical => "Cr�tica",
                _ => priority.ToString()
            };
        }

        private static string GetStatusLabel(TicketStatus status)
        {
            return status switch
            {
                TicketStatus.Open => "Abierto",
                TicketStatus.InProgress => "En Progreso",
                TicketStatus.Resolved => "Resuelto",
                TicketStatus.Closed => "Cerrado",
                _ => status.ToString()
            };
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalMinutes < 0)
            {
                span = TimeSpan.Zero;
            }

            var days = (int)Math.Floor(span.TotalDays);
            var hours = span.Hours;
            var minutes = span.Minutes;

            if (days > 0)
            {
                return $"{days}d {hours}h {minutes}m";
            }

            if (hours > 0)
            {
                return $"{hours}h {minutes}m";
            }

            return $"{minutes}m";
        }

        private static string? Safe(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static string FormatCurrency(decimal? value, string symbol)
        {
            return value.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0} {1:N2}", symbol, value.Value)
                : "No aplica";
        }

        private sealed class TicketReportData
        {
            public string TicketNumber { get; init; } = string.Empty;
            public DateTime GeneratedAt { get; init; }
            public string StatusLabel { get; init; } = string.Empty;
            public string PriorityLabel { get; init; } = string.Empty;
            public string SlaCompliance { get; init; } = string.Empty;
            public string SlaColor { get; init; } = "#0B6E4F";
            public string ResolutionTimeLabel { get; init; } = string.Empty;
            public string ExecutiveSummary { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public IReadOnlyList<ReportRow> GeneralRows { get; init; } = Array.Empty<ReportRow>();
            public IReadOnlyList<ReportRow> TechnicalRows { get; init; } = Array.Empty<ReportRow>();
            public IReadOnlyList<EvidenceData> Evidences { get; init; } = Array.Empty<EvidenceData>();
            public IReadOnlyList<HistoryRow> HistoryRows { get; init; } = Array.Empty<HistoryRow>();
            public byte[]? LogoBytes { get; init; }
        }

        private sealed record ReportRow(string Label, string Value);

        private sealed record EvidenceData(string Title, byte[]? ImageBytes, string Description);

        private sealed record HistoryRow(
            string ChangeDate,
            string Field,
            string OldValue,
            string NewValue,
            string ChangedBy);
    }
}




